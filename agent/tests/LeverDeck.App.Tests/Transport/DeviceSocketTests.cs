using System.Net.WebSockets;
using LeverDeck.Domain.Actions;
using LeverDeck.Protocol;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using static LeverDeck.App.Tests.Transport.Clients;

namespace LeverDeck.App.Tests.Transport;

/// <summary><c>GET /ws</c>: the device channel. Every key press goes to the recording executor, never to SendInput.</summary>
public sealed class DeviceSocketTests
{
    [Fact]
    public async Task Connect_WithoutAFirstMessage_ClosesAfterTheTimeout()
    {
        using var factory = new TransportAppFactory(TransportAppFactory.ShortConnectTimeout);
        using var device = await OpenDeviceSocketAsync(factory);

        var seen = await device.ReceiveUntilClosedAsync();

        var error = Assert.IsType<ErrorMessage>(Assert.Single(seen));
        Assert.Null(error.RequestId);
        Assert.Equal(ErrorCodeDto.Unauthorized, error.Code);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
        Assert.Equal("unauthorized", device.Socket.CloseStatusDescription);
    }

    [Fact]
    public async Task Connect_WithAnotherFirstMessage_IsUnauthorizedAndCloses()
    {
        using var factory = new TransportAppFactory();
        using var device = await OpenDeviceSocketAsync(factory);
        var requestId = NewRequestId();

        await device.SendAsync(new GetProfilesQuery(requestId));
        var seen = await device.ReceiveUntilClosedAsync();

        var error = Assert.IsType<ErrorMessage>(Assert.Single(seen));
        Assert.Equal(requestId, error.RequestId);
        Assert.Equal(ErrorCodeDto.Unauthorized, error.Code);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
    }

    [Fact]
    public async Task Connect_WithMalformedJson_IsUnauthorizedAndCloses()
    {
        using var factory = new TransportAppFactory();
        using var device = await OpenDeviceSocketAsync(factory);

        await device.SendTextAsync("{ not json");
        var seen = await device.ReceiveUntilClosedAsync();

        var error = Assert.IsType<ErrorMessage>(Assert.Single(seen));
        Assert.Null(error.RequestId);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
    }

    [Fact]
    public async Task Connect_WithAnUnknownToken_IsUnauthorizedAndCloses()
    {
        using var factory = new TransportAppFactory();
        using var device = await OpenDeviceSocketAsync(factory);

        await device.SendAsync(new ConnectDeviceCommand(NewRequestId(), new ConnectDevicePayload("not-a-token", ProtocolInfo.ProtocolVersion)));
        var seen = await device.ReceiveUntilClosedAsync();

        Assert.Equal(ErrorCodeDto.Unauthorized, Assert.IsType<ErrorMessage>(Assert.Single(seen)).Code);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
        Assert.Equal("unauthorized", device.Socket.CloseStatusDescription);
    }

    [Fact]
    public async Task Connect_WithAnotherProtocolVersion_IsProtocolMismatchAndCloses()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await OpenDeviceSocketAsync(factory);

        await device.SendAsync(new ConnectDeviceCommand(NewRequestId(), new ConnectDevicePayload(paired.DeviceToken, ProtocolInfo.ProtocolVersion + 1)));
        var seen = await device.ReceiveUntilClosedAsync();

        Assert.Equal(ErrorCodeDto.ProtocolMismatch, Assert.IsType<ErrorMessage>(Assert.Single(seen)).Code);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, device.Socket.CloseStatus);
        Assert.Equal("protocolMismatch", device.Socket.CloseStatusDescription);
    }

    [Fact]
    public async Task PairConnectAndPress_ReachesTheExecutor()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        var profiles = Assert.IsType<ProfilesResult>(await device.RequestAsync(new GetProfilesQuery(NewRequestId())));
        var summary = Assert.Single(profiles.Payload.Profiles);
        var profile = Assert.IsType<ProfileResult>(
            await device.RequestAsync(new GetProfileQuery(NewRequestId(), new ProfileIdPayload(summary.Id)))).Payload.Profile;
        var page = profile.Pages[0];
        var button = page.Buttons.First(candidate => candidate.Action is MediaKeyActionDto);

        var response = await device.RequestAsync(
            new ExecuteActionCommand(NewRequestId(), new ExecuteActionPayload(profile.Id, page.Index, button.Id)));

        Assert.IsType<AckMessage>(response);
        var executed = Assert.IsType<MediaKey>(Assert.Single(factory.Executor.Executed));
        Assert.Equal(((MediaKeyActionDto)button.Action).Key.ToString(), executed.Key.ToString());
    }

    [Fact]
    public async Task Device_SendingAnAdminCommand_IsForbiddenAndStaysConnected()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        var forbidden = Assert.IsType<ErrorMessage>(await device.RequestAsync(new StartPairingCommand(NewRequestId())));
        var devices = Assert.IsType<ErrorMessage>(await device.RequestAsync(new GetDevicesQuery(NewRequestId())));

        Assert.Equal(ErrorCodeDto.Forbidden, forbidden.Code);
        Assert.Equal(ErrorCodeDto.Forbidden, devices.Code);
        var games = Assert.IsType<ErrorMessage>(await device.RequestAsync(new GetInstalledGamesQuery(NewRequestId())));
        Assert.Equal(ErrorCodeDto.Forbidden, games.Code);
        Assert.IsType<AgentStatusResult>(await device.RequestAsync(new GetAgentStatusQuery(NewRequestId())));
    }

    [Fact]
    public async Task Device_SendingMalformedMessages_GetsValidationFailedAndStaysConnected()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        await device.SendTextAsync("{ not json");
        var malformed = await device.ReceiveAsync<ErrorMessage>();
        await device.SendTextAsync("""{"kind":"command","type":"launchMissiles","requestId":"r-1","payload":{}}""");
        var unknown = await device.ReceiveAsync<ErrorMessage>();
        await device.SendTextAsync("""{"kind":"ack","requestId":"r-2"}""");
        var wrongKind = await device.ReceiveAsync<ErrorMessage>();

        Assert.Equal(ErrorCodeDto.ValidationFailed, malformed.Code);
        Assert.Null(malformed.RequestId);
        Assert.Equal(ErrorCodeDto.ValidationFailed, unknown.Code);
        Assert.Equal("r-1", unknown.RequestId);
        Assert.Equal(ErrorCodeDto.ValidationFailed, wrongKind.Code);
        Assert.IsType<AgentStatusResult>(await device.RequestAsync(new GetAgentStatusQuery(NewRequestId())));
    }

    [Fact]
    public async Task Device_SendingAnOversizedMessage_IsClosed()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        await device.SendTextAsync(new string(' ', 70 * 1024));
        await device.ReceiveUntilClosedAsync();

        Assert.Equal(WebSocketCloseStatus.MessageTooBig, device.Socket.CloseStatus);
    }

    [Fact]
    public async Task Ws_WithoutAnUpgrade_IsBadRequest()
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/ws", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
