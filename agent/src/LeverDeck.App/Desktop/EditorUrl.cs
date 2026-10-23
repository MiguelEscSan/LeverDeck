using LeverDeck.Infrastructure.Configuration;

namespace LeverDeck.App.Desktop;

/// <summary>
/// The desktop editor's address with the admin token (docs/ARCHITECTURE.md, Desktop editor). The PWA moves the token
/// out of the URL and opens <c>/admin</c>, so the owner never sees it.
/// </summary>
internal static class EditorUrl
{
    public static string For(bool httpsEnabled, string adminToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminToken);
        var scheme = httpsEnabled ? "https" : "http";
        return $"{scheme}://localhost:{LeverDeckOptions.Port}/?admin={Uri.EscapeDataString(adminToken)}";
    }
}
