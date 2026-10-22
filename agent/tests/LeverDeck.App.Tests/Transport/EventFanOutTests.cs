using System.Net.WebSockets;
using LeverDeck.Protocol;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using static LeverDeck.App.Tests.Transport.Clients;

namespace LeverDeck.App.Tests.Transport;

/// <summary>Events reach the right connections: profiles go to everyone, the rest only to the editor.</summary>
public sealed class EventFanOutTests
{
    [Fact]
    public async Task SavingAProfile_PushesProfileUpdatedToTheDevice()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);
        var summary = Assert.Single(Assert.IsType<ProfilesResult>(await admin.RequestAsync(new GetProfilesQuery(NewRequestId()))).Payload.Profiles);
        var profile = Assert.IsType<ProfileResult>(
            await admin.RequestAsync(new GetProfileQuery(NewRequestId(), new ProfileIdPayload(summary.Id)))).Payload.Profile;

        var saved = await admin.RequestAsync(new SaveProfileCommand(NewRequestId(), new ProfilePayload(profile with { Name = "Streaming" })));
        var pushed = await device.ReceiveAsync<ProfileUpdatedEvent>();

        Assert.IsType<AckMessage>(saved);
        Assert.Equal(profile.Id, pushed.Payload.Profile.Id);
        Assert.Equal("Streaming", pushed.Payload.Profile.Name);
    }

    [Fact]
    public async Task ConnectingADevice_PushesDevicesChangedWithConnectedToTheEditor()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);

        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);
        var changed = await admin.ReceiveAsync<DevicesChangedEvent>(
            candidate => candidate.Payload.Devices.Any(dto => dto.Id == paired.DeviceId && dto.Connected));

        Assert.False(changed.Payload.Devices.Single().Revoked);
    }

    [Fact]
    public async Task StartingPairing_PushesThePairingSessionToTheEditorOnly()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        var requestId = NewRequestId();

        // Events are pushed while the command runs, so the editor sees the new session before the ack.
        await admin.SendAsync(new StartPairingCommand(requestId));
        var pushed = await admin.ReceiveAsync<PairingSessionChangedEvent>(candidate => candidate.Payload.Session is not null);
        await admin.ReceiveAsync<AckMessage>(candidate => candidate.RequestId == requestId);
        await device.SendAsync(new GetAgentStatusQuery(NewRequestId()));
        var first = await device.ReceiveAsync();

        Assert.StartsWith("http://leverdeck.local:8765/pair?code=", pushed.Payload.Session!.QrUrl, StringComparison.Ordinal);
        // The very next message the device gets is its answer: no admin-only event leaked to it.
        Assert.IsType<AgentStatusResult>(first);
    }

    [Fact]
    public async Task RevokingADevice_SendsDeviceRevokedThenClosesItsSocket()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        var revoked = await admin.RequestAsync(new RevokeDeviceCommand(NewRequestId(), new DeviceIdPayload(paired.DeviceId)));
        var seen = await device.ReceiveUntilClosedAsync();

        Assert.IsType<AckMessage>(revoked);
        var last = Assert.IsType<DeviceRevokedEvent>(seen[^1]);
        Assert.Equal(paired.DeviceId, last.Payload.DeviceId);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
        Assert.Equal("deviceRevoked", device.Socket.CloseStatusDescription);

        using var again = await OpenDeviceSocketAsync(factory);
        await again.SendAsync(new ConnectDeviceCommand(NewRequestId(), new ConnectDevicePayload(paired.DeviceToken, ProtocolInfo.ProtocolVersion)));
        var rejected = await again.ReceiveUntilClosedAsync();
        Assert.Equal(ErrorCodeDto.DeviceRevoked, Assert.IsType<ErrorMessage>(Assert.Single(rejected)).Code);
        Assert.Equal("deviceRevoked", again.Socket.CloseStatusDescription);
    }
}
