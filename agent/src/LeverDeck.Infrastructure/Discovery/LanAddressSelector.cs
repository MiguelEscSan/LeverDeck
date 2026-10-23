using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>What the selector needs to know about a network interface.</summary>
internal sealed record LanInterface(
    OperationalStatus Status,
    NetworkInterfaceType Type,
    IReadOnlyList<IPAddress> Addresses,
    bool HasIpv4Gateway);

/// <summary>Picks the IPv4 address that this PC's <see cref="AgentHostName"/> should resolve to.</summary>
internal static class LanAddressSelector
{
    /// <summary>
    /// The first usable IPv4 address of an interface that is up, preferring one with a default gateway (the LAN
    /// adapter) over host-only ones such as Hyper-V or WSL switches. Loopback, tunnels and link-local are skipped.
    /// </summary>
    public static IPAddress? Select(IEnumerable<LanInterface> interfaces)
    {
        var usable = interfaces
            .Where(candidate => candidate.Status == OperationalStatus.Up
                && candidate.Type is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(candidate => (candidate.HasIpv4Gateway, Address: candidate.Addresses.FirstOrDefault(IsUsable)))
            .Where(candidate => candidate.Address is not null)
            .ToList();

        return usable.Find(candidate => candidate.HasIpv4Gateway).Address ?? usable.FirstOrDefault().Address;
    }

    /// <summary>The address as a DNS <c>IP4_ADDRESS</c>: a 32-bit value whose bytes are in network order in memory.</summary>
    public static uint ToIp4Address(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Only IPv4 addresses are supported.", nameof(address));
        }

        return BitConverter.ToUInt32(address.GetAddressBytes());
    }

    public static IReadOnlyList<LanInterface> FromSystem() =>
    [
        .. NetworkInterface.GetAllNetworkInterfaces().Select(adapter =>
        {
            var properties = adapter.GetIPProperties();
            return new LanInterface(
                adapter.OperationalStatus,
                adapter.NetworkInterfaceType,
                [.. properties.UnicastAddresses.Select(unicast => unicast.Address)],
                properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any)));
        }),
    ];

    private static bool IsUsable(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 169 && bytes[1] == 254);
    }
}
