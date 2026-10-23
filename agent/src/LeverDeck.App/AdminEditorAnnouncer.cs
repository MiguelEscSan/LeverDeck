using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Security;

namespace LeverDeck.App;

/// <summary>
/// Creates the admin token on start (if needed). Debug builds log the editor URL with the token once, so
/// <c>dotnet run</c> can open the editor in a browser; the PWA moves the token out of the URL and opens <c>/admin</c>.
/// Release builds log to a file next to the DPAPI-protected token, so they never write the token there: the owner
/// opens the editor from the tray or the Start menu instead.
/// </summary>
internal sealed partial class AdminEditorAnnouncer(
    AdminTokenStore adminTokens,
    LeverDeckOptions options,
    ILogger<AdminEditorAnnouncer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var scheme = options.HttpsEnabled ? "https" : "http";
#if DEBUG
        var token = adminTokens.GetOrCreate();
        LogEditorUrl(scheme, LeverDeckOptions.Port, token);
#else
        adminTokens.GetOrCreate();
        LogEditorFromTray(scheme, LeverDeckOptions.Port);
#endif
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

#if DEBUG
    [LoggerMessage(Level = LogLevel.Information, Message = "Desktop editor: {Scheme}://localhost:{Port}/?admin={AdminToken}")]
    private partial void LogEditorUrl(string scheme, int port, string adminToken);
#else
    [LoggerMessage(Level = LogLevel.Information, Message = "Desktop editor: open LeverDeck from the tray icon or the Start menu (it serves {Scheme}://localhost:{Port}).")]
    private partial void LogEditorFromTray(string scheme, int port);
#endif
}
