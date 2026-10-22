using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LeverDeck.App.Tests;

/// <summary>
/// The embedded PWA: <c>GET /</c>, the SPA fallback and cache headers. The agent embeds the Angular build when
/// <c>web/dist</c> exists at build time (CI builds it first) and a fallback page otherwise; the tests read which one
/// from the assembly metadata and check Angular-only files only when the Angular build is embedded.
/// </summary>
public sealed partial class WebUiTests(LeverDeckAppFactory factory) : IClassFixture<LeverDeckAppFactory>
{
    private static readonly string EmbeddedWebUi = typeof(Program).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "LeverDeck.WebUi")
        .Value!;

    private static bool AngularEmbedded => EmbeddedWebUi == "angular";

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var client = factory.CreateClient();
        return await client.GetAsync(new Uri(path, UriKind.Relative));
    }

    [Fact]
    public void EmbeddedWebUi_IsAngularOrTheFallbackPage()
    {
        Assert.True(EmbeddedWebUi is "angular" or "fallback", $"Unexpected LeverDeck.WebUi metadata '{EmbeddedWebUi}'.");
    }

    [Fact]
    public async Task Root_ReturnsTheEmbeddedPage()
    {
        using var response = await GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains(AngularEmbedded ? "<app-root>" : "name=\"leverdeck-web-ui\" content=\"fallback\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/deck")]
    [InlineData("/settings/devices")]
    public async Task ClientRoute_FallsBackToIndex(string path)
    {
        using var root = await GetAsync("/");
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal(await root.Content.ReadAsStringAsync(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_IsNotShadowed()
    {
        using var response = await GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/pairing/redeem")]
    [InlineData("/api")]
    [InlineData("/ws")]
    [InlineData("/admin/ws")]
    [InlineData("/debug/execute")]
    [InlineData("/health/details")]
    [InlineData("/missing.js")]
    public async Task ReservedOrMissingPaths_AreNotFound(string path)
    {
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonGetOnClientRoute_IsNotFound()
    {
        using var client = factory.CreateClient();
        using var body = new StringContent("{}");

        using var response = await client.PostAsync(new Uri("/deck", UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/index.html", "text/html")]
    [InlineData("/ngsw.json", "application/json")]
    [InlineData("/ngsw-worker.js", "text/javascript")]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    public async Task ServiceWorkerAndManifestFiles_AreRevalidated(string path, string mediaType)
    {
        if (!AngularEmbedded)
        {
            return; // only the Angular build has these files
        }

        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task HashedBundles_AreCachedForever()
    {
        if (!AngularEmbedded)
        {
            return; // the fallback page has no bundles
        }

        using var index = await GetAsync("/");
        var bundles = BundleReference().Matches(await index.Content.ReadAsStringAsync())
            .Select(match => match.Groups["file"].Value)
            .ToList();
        Assert.NotEmpty(bundles);

        foreach (var bundle in bundles)
        {
            using var response = await GetAsync("/" + bundle);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
        }
    }

    [GeneratedRegex(@"(?:src|href)=""(?<file>[a-z]+-[A-Z0-9]{8}\.(?:js|css))""")]
    private static partial Regex BundleReference();
}
