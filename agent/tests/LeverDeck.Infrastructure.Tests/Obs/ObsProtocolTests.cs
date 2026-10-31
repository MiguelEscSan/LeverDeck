using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeverDeck.Infrastructure.Obs;

namespace LeverDeck.Infrastructure.Tests.Obs;

public sealed class ObsProtocolTests
{
    [Fact]
    public void Authentication_FollowsTheObsWebSocketRecipe()
    {
        // base64(sha256(base64(sha256(password + salt)) + challenge)), written out here from the protocol document.
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("pw" + "salt")));
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + "challenge")));

        Assert.Equal(expected, ObsProtocol.Authentication("pw", "salt", "challenge"));
    }

    [Fact]
    public void Identify_WithoutAPassword_OmitsAuthentication()
    {
        using var frame = JsonDocument.Parse(ObsProtocol.Identify(null));
        var data = frame.RootElement.GetProperty("d");

        Assert.Equal(ObsProtocol.OpIdentify, frame.RootElement.GetProperty("op").GetInt32());
        Assert.Equal(ObsProtocol.RpcVersion, data.GetProperty("rpcVersion").GetInt32());
        Assert.Equal(ObsProtocol.EventSubscriptions, data.GetProperty("eventSubscriptions").GetInt32());
        Assert.False(data.TryGetProperty("authentication", out _));
    }

    [Fact]
    public void Identify_WithAuthentication_CarriesIt()
    {
        using var frame = JsonDocument.Parse(ObsProtocol.Identify("auth"));

        Assert.Equal("auth", frame.RootElement.GetProperty("d").GetProperty("authentication").GetString());
    }

    [Fact]
    public void Request_CarriesTypeIdAndData()
    {
        using var frame = JsonDocument.Parse(ObsProtocol.Request("r1", "SetCurrentProgramScene", writer => writer.WriteString("sceneName", "Gameplay")));
        var data = frame.RootElement.GetProperty("d");

        Assert.Equal(ObsProtocol.OpRequest, frame.RootElement.GetProperty("op").GetInt32());
        Assert.Equal("SetCurrentProgramScene", data.GetProperty("requestType").GetString());
        Assert.Equal("r1", data.GetProperty("requestId").GetString());
        Assert.Equal("Gameplay", data.GetProperty("requestData").GetProperty("sceneName").GetString());
    }

    [Fact]
    public void Request_WithoutData_OmitsRequestData()
    {
        using var frame = JsonDocument.Parse(ObsProtocol.Request("r2", "StartStream"));

        Assert.False(frame.RootElement.GetProperty("d").TryGetProperty("requestData", out _));
    }
}
