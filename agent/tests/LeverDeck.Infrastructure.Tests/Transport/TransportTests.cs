using System.Net;
using System.Net.WebSockets;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Pairing;
using LeverDeck.Infrastructure.Discovery;
using LeverDeck.Infrastructure.Tests.Discovery;
using LeverDeck.Infrastructure.Transport;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Tests.Transport;

public sealed class PairingLinksTests
{
    private static readonly PairingCode Code = new("042137");

    private static readonly AgentHostName Studio = AgentHostNames.Of("leverdeck-studio");

    [Fact]
    public void QrUrl_UsesThisPcsMdnsName()
    {
        var links = new PairingLinks(Studio, () => null, "http");

        Assert.Equal("http://leverdeck-studio.local:8765/pair?code=042137", links.QrUrl(Code));
    }

    [Fact]
    public void QrUrl_UsesHttpsWhenTheAgentServesHttps()
    {
        var links = new PairingLinks(Studio, () => null, "https");

        Assert.Equal("https://leverdeck-studio.local:8765/pair?code=042137", links.QrUrl(Code));
    }

    [Fact]
    public void FallbackUrl_UsesTheCurrentLanAddress()
    {
        var links = new PairingLinks(Studio, () => IPAddress.Parse("192.168.1.20"), "https");

        Assert.Equal("https://192.168.1.20:8765/pair?code=042137", links.FallbackUrl(Code));
    }

    [Fact]
    public void FallbackUrl_IsNullWithoutALanAddress()
    {
        var links = new PairingLinks(Studio, () => null, "http");

        Assert.Null(links.FallbackUrl(Code));
    }
}

public sealed class ConnectionRegistryTests
{
    private static WebSocketConnection Connection(Caller? caller = null)
    {
        var socket = WebSocket.CreateFromStream(new MemoryStream(), new WebSocketCreationOptions { IsServer = true });
        var connection = new WebSocketConnection(socket, 1024, CancellationToken.None);
        if (caller is not null)
        {
            connection.Bind(caller);
        }

        return connection;
    }

    [Fact]
    public void Add_RejectsAnUnauthenticatedConnection()
    {
        using var connection = Connection();

        Assert.Throws<InvalidOperationException>(() => new ConnectionRegistry().Add(connection));
    }

    [Fact]
    public void Registry_TracksDevicesAndAdminsUntilRemoved()
    {
        var phone = new DeviceId(Guid.NewGuid());
        var tablet = new DeviceId(Guid.NewGuid());
        using var device = Connection(Caller.ForDevice(phone));
        using var admin = Connection(Caller.Admin);
        var registry = new ConnectionRegistry();

        registry.Add(device);
        registry.Add(admin);

        Assert.True(registry.IsConnected(phone));
        Assert.False(registry.IsConnected(tablet));
        Assert.Same(admin, Assert.Single(registry.Admins()));
        Assert.Same(device, Assert.Single(registry.OfDevice(phone)));
        Assert.Equal(2, registry.All().Count);

        registry.Remove(device);

        Assert.False(registry.IsConnected(phone));
        Assert.Empty(registry.OfDevice(phone));
    }

    [Fact]
    public async Task IsConnected_IsFalseOnceTheConnectionIsClosing()
    {
        var phone = new DeviceId(Guid.NewGuid());
        using var device = Connection(Caller.ForDevice(phone));
        var registry = new ConnectionRegistry();
        registry.Add(device);

        await device.CloseAsync(WebSocketCloseStatus.PolicyViolation, CloseReasons.For(ErrorCodeDto.DeviceRevoked));

        Assert.True(device.IsClosing);
        Assert.False(registry.IsConnected(phone));
    }
}

public sealed class CloseReasonsTests
{
    [Theory]
    [InlineData(ErrorCodeDto.Unauthorized, "unauthorized")]
    [InlineData(ErrorCodeDto.DeviceRevoked, "deviceRevoked")]
    [InlineData(ErrorCodeDto.ProtocolMismatch, "protocolMismatch")]
    public void For_IsTheWireErrorCode(ErrorCodeDto code, string expected) => Assert.Equal(expected, CloseReasons.For(code));
}
