using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Security;

namespace LeverDeck.App;

/// <summary>
/// Creates the admin token on start (if needed) and logs the editor URL once, so the owner can open the editor until
/// the tray icon does it (Phase 7). The editor itself arrives later; the path is a placeholder.
/// </summary>
internal sealed partial class AdminEditorAnnouncer(AdminTokenStore adminTokens, ILogger<AdminEditorAnnouncer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var token = adminTokens.GetOrCreate();
        LogEditorUrl(LeverDeckOptions.Port, token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Desktop editor: http://localhost:{Port}/?admin={AdminToken}")]
    private partial void LogEditorUrl(int port, string adminToken);
}
