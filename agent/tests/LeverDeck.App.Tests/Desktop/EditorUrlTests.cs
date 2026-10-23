using LeverDeck.App.Desktop;

namespace LeverDeck.App.Tests.Desktop;

public sealed class EditorUrlTests
{
    [Fact]
    public void For_OpensTheAppOnLocalhostWithTheAdminToken()
    {
        Assert.Equal("http://localhost:8765/?admin=abc-123_XYZ", EditorUrl.For(httpsEnabled: false, "abc-123_XYZ"));
    }

    [Fact]
    public void For_UsesHttpsWhenTheAgentServesIt()
    {
        Assert.StartsWith("https://localhost:8765/", EditorUrl.For(httpsEnabled: true, "token"), StringComparison.Ordinal);
    }

    [Fact]
    public void For_EscapesTheToken()
    {
        Assert.Equal("http://localhost:8765/?admin=a%2Bb%2Fc%3D", EditorUrl.For(httpsEnabled: false, "a+b/c="));
    }
}
