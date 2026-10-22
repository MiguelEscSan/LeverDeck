using System.Net;
using System.Net.Http.Json;
using System.Text;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Serialization;

namespace LeverDeck.App.Tests.Transport;

public sealed class PairingRedeemEndpointTests
{
    private static readonly Uri RedeemUri = new("/api/pairing/redeem", UriKind.Relative);

    private static async Task<ErrorDto> ReadErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.ErrorDto))!;

    [Fact]
    public async Task Redeem_WithTheActiveCode_ReturnsTheDeviceTokenOnce()
    {
        using var factory = new TransportAppFactory();
        using var admin = await Clients.ConnectAdminAsync(factory);
        var code = await Clients.StartPairingAsync(admin);

        using var response = await Clients.RedeemAsync(factory, code, "Pixel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var paired = (await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.PairDeviceResponse))!;
        Assert.NotEqual(Guid.Empty, paired.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(paired.DeviceToken));

        using var again = await Clients.RedeemAsync(factory, code, "Pixel");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(ErrorCodeDto.PairingCodeInvalid, (await ReadErrorAsync(again)).Code);
    }

    [Fact]
    public async Task Redeem_WithoutASession_IsPairingCodeInvalid()
    {
        using var factory = new TransportAppFactory();

        using var response = await Clients.RedeemAsync(factory, "123456", "Pixel");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodeDto.PairingCodeInvalid, (await ReadErrorAsync(response)).Code);
    }

    [Fact]
    public async Task Redeem_AfterTheSessionExpired_IsPairingCodeExpired()
    {
        using var factory = new TransportAppFactory();
        using var admin = await Clients.ConnectAdminAsync(factory);
        var code = await Clients.StartPairingAsync(admin);
        factory.Time.Advance(TimeSpan.FromMinutes(6));

        using var response = await Clients.RedeemAsync(factory, code, "Pixel");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodeDto.PairingCodeExpired, (await ReadErrorAsync(response)).Code);
    }

    [Fact]
    public async Task Redeem_WithAnInvalidName_IsUnprocessable()
    {
        using var factory = new TransportAppFactory();
        using var admin = await Clients.ConnectAdminAsync(factory);
        var code = await Clients.StartPairingAsync(admin);

        using var response = await Clients.RedeemAsync(factory, code, "   ");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodeDto.ValidationFailed, (await ReadErrorAsync(response)).Code);
    }

    [Fact]
    public async Task Redeem_WithMalformedJson_IsBadRequest()
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();
        using var body = new StringContent("""{"code":"123456"}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(RedeemUri, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodeDto.ValidationFailed, (await ReadErrorAsync(response)).Code);
    }

    [Fact]
    public async Task Redeem_WithoutAJsonContentType_IsUnsupportedMediaType()
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();
        using var body = new StringContent("""{"code":"123456","deviceName":"Pixel"}""", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(RedeemUri, body);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Redeem_WithAnOversizedBody_IsRejected()
    {
        using var factory = new TransportAppFactory();
        using var client = factory.CreateClient();
        var name = new string('a', 64 * 1024);
        using var body = new StringContent($$"""{"code":"123456","deviceName":"{{name}}"}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(RedeemUri, body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
