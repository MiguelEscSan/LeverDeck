namespace LeverDeck.App.Tests;

public sealed class WebUiCachePolicyTests
{
    [Theory]
    [InlineData("/main-7W3HWPXS.js")]
    [InlineData("/styles-UNMMASH6.css")]
    [InlineData("/chunk-AB12CD34.js")]
    [InlineData("/polyfills-ZZZZ9999.js")]
    public void HashedBundles_AreImmutable(string path)
    {
        Assert.Equal(WebUiCachePolicy.Immutable, WebUiCachePolicy.For(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/ngsw.json")]
    [InlineData("/ngsw-worker.js")]
    [InlineData("/safety-worker.js")]
    [InlineData("/worker-basic.min.js")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/icons/icon-192x192.png")]
    [InlineData("/main.js")]
    [InlineData("/main-lowercase.js")]
    public void EverythingElse_IsRevalidated(string path)
    {
        Assert.Equal(WebUiCachePolicy.NoCache, WebUiCachePolicy.For(path));
    }

    [Theory]
    [InlineData("/api", true)]
    [InlineData("/api/pairing/redeem", true)]
    [InlineData("/API/x", true)]
    [InlineData("/ws", true)]
    [InlineData("/admin/ws", true)]
    [InlineData("/debug/execute", true)]
    [InlineData("/health", true)]
    [InlineData("/apis", false)]
    [InlineData("/wsx", false)]
    [InlineData("/admin", false)]
    [InlineData("/deck", false)]
    [InlineData("/", false)]
    public void ReservedPaths_AreNeverTheSpa(string path, bool reserved)
    {
        Assert.Equal(reserved, WebUi.IsReserved(path));
    }
}
