using System.Buffers.Binary;
using System.Net;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using static LeverDeck.App.Tests.Transport.Clients;

namespace LeverDeck.App.Tests.Transport;

/// <summary>Uploading an icon over <c>/admin/ws</c>, serving it at <c>/api/icons/&lt;id&gt;.png</c> and deleting it.</summary>
public sealed class IconTests
{
    private const string IconId = "custom-0123456789abcdef0123456789abcdef";

    private static byte[] Png()
    {
        var bytes = new byte[40];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 256);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 256);
        return bytes;
    }

    private static SaveIconCommand Upload(string iconId = IconId) =>
        new(NewRequestId(), new SaveIconPayload(iconId, Convert.ToBase64String(Png())));

    [Fact]
    public async Task Upload_IsListedPushedAndServedAsACachedPng()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);

        // The agent pushes the event before the ack of the command that caused it.
        await admin.SendAsync(Upload());
        var pushed = await admin.ReceiveAsync<IconsChangedEvent>();
        await admin.ReceiveAsync<AckMessage>();
        var listed = Assert.IsType<IconsResult>(await admin.RequestAsync(new GetIconsQuery(NewRequestId())));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri($"/api/icons/{IconId}.png", UriKind.Relative));

        Assert.Equal([IconId], pushed.Payload.Icons);
        Assert.Equal([IconId], listed.Payload.Icons);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal(Png(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_OfSomethingThatIsNotAPng_IsValidationFailed()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);

        var error = Assert.IsType<ErrorMessage>(await admin.RequestAsync(
            new SaveIconCommand(NewRequestId(), new SaveIconPayload(IconId, Convert.ToBase64String("<svg/>"u8.ToArray())))));

        Assert.Equal(ErrorCodeDto.ValidationFailed, error.Code);
    }

    [Theory]
    [InlineData("/api/icons/custom-0123456789abcdef0123456789abcdef.png")]
    [InlineData("/api/icons/play-pause.png")]
    [InlineData("/api/icons/..%2Fsettings.json")]
    [InlineData("/api/icons/custom-0123456789abcdef0123456789abcdef")]
    public async Task Get_OfAnythingButAStoredIcon_IsNotFound(string path)
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheIcon()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        await admin.RequestAsync(Upload());

        Assert.IsType<AckMessage>(await admin.RequestAsync(new DeleteIconCommand(NewRequestId(), new IconIdPayload(IconId))));
        var listed = Assert.IsType<IconsResult>(await admin.RequestAsync(new GetIconsQuery(NewRequestId())));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri($"/api/icons/{IconId}.png", UriKind.Relative));

        Assert.Empty(listed.Payload.Icons);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Device_CannotUploadIcons()
    {
        using var factory = new TransportAppFactory();
        using var admin = await ConnectAdminAsync(factory);
        var paired = await PairAsync(factory, admin);
        using var device = await ConnectDeviceAsync(factory, paired.DeviceToken);

        var error = Assert.IsType<ErrorMessage>(await device.RequestAsync(Upload()));

        Assert.Equal(ErrorCodeDto.Forbidden, error.Code);
    }
}
