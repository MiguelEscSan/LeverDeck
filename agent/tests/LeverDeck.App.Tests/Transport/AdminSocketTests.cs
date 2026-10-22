using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using static LeverDeck.App.Tests.Transport.Clients;

namespace LeverDeck.App.Tests.Transport;

/// <summary><c>GET /admin/ws</c>: the desktop editor channel, guarded by the persistent admin token.</summary>
public sealed class AdminSocketTests
{
    [Fact]
    public async Task Connect_WithoutAToken_IsRejectedBeforeTheUpgrade()
    {
        using var factory = new TransportAppFactory();

        await Assert.ThrowsAnyAsync<Exception>(() => factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/admin/ws"), CancellationToken.None));
    }

    [Fact]
    public async Task Connect_WithAWrongToken_IsRejectedBeforeTheUpgrade()
    {
        using var factory = new TransportAppFactory();

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAdminAsync(factory, "wrong-token"));
    }

    [Fact]
    public async Task Connect_WithoutAnUpgrade_IsUnauthorizedOrBadRequest()
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/admin/ws", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPairingSession_CarriesTheQrAndFallbackUrls()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        Assert.IsType<AckMessage>(await admin.RequestAsync(new StartPairingCommand(NewRequestId())));

        var result = Assert.IsType<PairingSessionResult>(await admin.RequestAsync(new GetPairingSessionQuery(NewRequestId())));

        var session = Assert.IsType<PairingSessionDto>(result.Payload.Session);
        Assert.Equal($"http://leverdeck.local:8765/pair?code={session.Code}", session.QrUrl);
        if (session.FallbackUrl is { } fallback)
        {
            Assert.Matches(@"^http://\d{1,3}(\.\d{1,3}){3}:8765/pair\?code=\d+$", fallback);
        }
    }

    [Fact]
    public async Task GetDevices_ReportsWhichDevicesAreConnected()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var online = await PairAsync(factory, admin, "Online");
        var offline = await PairAsync(factory, admin, "Offline");
        using var device = await ConnectDeviceAsync(factory, online.DeviceToken);

        var result = Assert.IsType<DevicesResult>(await admin.RequestAsync(new GetDevicesQuery(NewRequestId())));

        Assert.True(result.Payload.Devices.Single(candidate => candidate.Id == online.DeviceId).Connected);
        Assert.False(result.Payload.Devices.Single(candidate => candidate.Id == offline.DeviceId).Connected);
    }

    [Fact]
    public async Task Admin_SendingConnectDevice_IsValidationFailed()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);

        var error = Assert.IsType<ErrorMessage>(await admin.RequestAsync(
            new ConnectDeviceCommand(NewRequestId(), new ConnectDevicePayload("token", 1))));

        Assert.Equal(ErrorCodeDto.ValidationFailed, error.Code);
    }

    [Fact]
    public async Task Admin_CanRegisterAnApp_AndGetsItsIdInTheAck()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);

        var requestId = NewRequestId();

        // The event is pushed while the command runs, so it arrives before the ack.
        await admin.SendAsync(new RegisterAppCommand(requestId, new RegisterAppPayload("Notepad", @"C:\Windows\notepad.exe")));
        var settings = await admin.ReceiveAsync<SettingsUpdatedEvent>();
        var ack = await admin.ReceiveAsync<AckMessage>(candidate => candidate.RequestId == requestId);

        Assert.NotNull(ack.Payload);
        Assert.Contains(settings.Payload.Settings.Apps, app => app.AppId == ack.Payload.AppId);
    }
}
