using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Security;

namespace LeverDeck.App;

/// <summary>
/// Creates the admin token on start (if needed) and logs the editor URL once, so the owner can open the editor until
/// the tray icon does it (Phase 7). The PWA moves the token out of the URL and opens <c>/admin</c>.
/// </summary>
internal sealed partial class AdminEditorAnnouncer(
    AdminTokenStore adminTokens,
    LeverDeckOptions options,
    ILogger<AdminEditorAnnouncer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var token = adminTokens.GetOrCreate();
        LogEditorUrl(options.HttpsEnabled ? "https" : "http", LeverDeckOptions.Port, token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Desktop editor: {Scheme}://localhost:{Port}/?admin={AdminToken}")]
    private partial void LogEditorUrl(string scheme, int port, string adminToken);
}
