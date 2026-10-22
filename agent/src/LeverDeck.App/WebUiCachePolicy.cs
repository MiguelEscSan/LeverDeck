using System.Text.RegularExpressions;

namespace LeverDeck.App;

/// <summary>
/// <c>Cache-Control</c> for the embedded PWA. Angular puts a content hash in every bundle name
/// (<c>main-7W3HWPXS.js</c>), so those never change and are cached for a year. Everything else — <c>index.html</c>,
/// <c>ngsw.json</c>, <c>ngsw-worker.js</c>, the manifest, icons — keeps its name across releases and is revalidated
/// on every load (cheap on the LAN, and the service worker relies on fresh copies to detect updates).
/// </summary>
internal static partial class WebUiCachePolicy
{
    public const string NoCache = "no-cache";
    public const string Immutable = "public, max-age=31536000, immutable";

    public static string For(string path) => HashedBundle().IsMatch(path) ? Immutable : NoCache;

    [GeneratedRegex(@"/[a-z]+-[A-Z0-9]{8}\.(?:js|css)$", RegexOptions.CultureInvariant)]
    private static partial Regex HashedBundle();
}
