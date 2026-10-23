using System.Net;
using System.Net.NetworkInformation;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeverDeck.Infrastructure.Tests.Discovery;

public sealed class LanAddressSelectorTests
{
    private static LanInterface Interface(
        string address,
        bool gateway = true,
        OperationalStatus status = OperationalStatus.Up,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet) =>
        new(status, type, [IPAddress.Parse(address)], gateway);

    [Fact]
    public void Select_PrefersAnInterfaceWithAGateway()
    {
        var selected = LanAddressSelector.Select(
        [
            Interface("172.20.0.1", gateway: false),
            Interface("192.168.1.20"),
        ]);

        Assert.Equal(IPAddress.Parse("192.168.1.20"), selected);
    }

    [Fact]
    public void Select_FallsBackToAnInterfaceWithoutGateway()
    {
        Assert.Equal(IPAddress.Parse("10.0.0.5"), LanAddressSelector.Select([Interface("10.0.0.5", gateway: false)]));
    }

    [Fact]
    public void Select_IgnoresLoopbackDownTunnelLinkLocalAndIpv6()
    {
        var selected = LanAddressSelector.Select(
        [
            Interface("127.0.0.1", type: NetworkInterfaceType.Loopback),
            Interface("192.168.1.30", status: OperationalStatus.Down),
            Interface("10.8.0.2", type: NetworkInterfaceType.Tunnel),
            Interface("169.254.10.10"),
            new LanInterface(OperationalStatus.Up, NetworkInterfaceType.Ethernet, [IPAddress.IPv6Loopback], true),
        ]);

        Assert.Null(selected);
    }

    [Fact]
    public void ToIp4Address_KeepsNetworkByteOrderInMemory()
    {
        var value = LanAddressSelector.ToIp4Address(IPAddress.Parse("192.168.1.20"));

        Assert.Equal(new byte[] { 192, 168, 1, 20 }, BitConverter.GetBytes(value));
    }
}

public sealed class ServiceAdvertisementHostedServiceTests
{
    private sealed class RecordingAdvertiser(bool fail = false) : IServiceAdvertiser
    {
        public List<string> Calls { get; } = [];

        public Task AdvertiseAsync(CancellationToken cancellationToken)
        {
            Calls.Add("advertise");
            return fail ? Task.FromException(new InvalidOperationException("no network")) : Task.CompletedTask;
        }

        public Task WithdrawAsync(CancellationToken cancellationToken)
        {
            Calls.Add("withdraw");
            return Task.CompletedTask;
        }
    }

    private static ServiceAdvertisementHostedService NewService(IServiceAdvertiser advertiser, bool enabled) =>
        new(advertiser, new LeverDeckOptions { DataDirectory = Path.GetTempPath(), MdnsEnabled = enabled }, NullLogger<ServiceAdvertisementHostedService>.Instance);

    [Fact]
    public async Task Enabled_AdvertisesOnStartAndWithdrawsOnStop()
    {
        var advertiser = new RecordingAdvertiser();
        using var service = NewService(advertiser, enabled: true);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(["advertise", "withdraw"], advertiser.Calls);
    }

    [Fact]
    public async Task Disabled_NeverTouchesTheAdvertiser()
    {
        var advertiser = new RecordingAdvertiser();
        using var service = NewService(advertiser, enabled: false);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(advertiser.Calls);
    }

    [Fact]
    public async Task FailingAdvertiser_DoesNotStopTheAgent()
    {
        var advertiser = new RecordingAdvertiser(fail: true);
        using var service = NewService(advertiser, enabled: true);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(["advertise"], advertiser.Calls);
    }

    [Fact]
    public void AdvertisedNames_MatchTheArchitecture()
    {
        Assert.Equal("leverdeck-studio._http._tcp.local", DnsServiceAdvertiser.ServiceNameFor(AgentHostNames.Of("leverdeck-studio")));
        Assert.Equal(8765, LeverDeckOptions.Port);
    }
}
