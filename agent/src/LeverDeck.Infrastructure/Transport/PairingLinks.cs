using System.Net;
using LeverDeck.Domain.Pairing;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// The links a phone opens to pair (docs/ARCHITECTURE.md, Pairing and authentication): the QR carries the
/// <c>leverdeck.local</c> URL; the fallback uses the PC's current LAN IP for devices where <c>.local</c> does not
/// resolve. The IP is read on every call, so a pairing session started after an address change gets the new one.
/// </summary>
public sealed class PairingLinks
{
    public const string HostName = "leverdeck.local";

    private readonly Func<IPAddress?> _lanAddress;

    public PairingLinks()
        : this(() => LanAddressSelector.Select(LanAddressSelector.FromSystem()))
    {
    }

    internal PairingLinks(Func<IPAddress?> lanAddress)
    {
        _lanAddress = lanAddress;
    }

    public static string QrUrl(PairingCode code) => Url(HostName, code);

    /// <summary>The same link on the LAN IP, or <see langword="null"/> when the PC has no usable IPv4 address.</summary>
    public string? FallbackUrl(PairingCode code) => _lanAddress() is { } address ? Url(address.ToString(), code) : null;

    private static string Url(string host, PairingCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return $"http://{host}:{LeverDeckOptions.Port}/pair?code={Uri.EscapeDataString(code.Value)}";
    }
}
