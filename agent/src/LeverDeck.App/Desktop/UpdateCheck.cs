using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace LeverDeck.App.Desktop;

/// <summary>
/// Keeps an installed LeverDeck up to date from GitHub Releases (Velopack). A minute after the start, and once a day
/// after that (a tray session can last weeks), it looks for a newer release and downloads it; <c>VelopackApp.Run</c>
/// applies it at the next start of the agent, so a running session is never interrupted. A pre-release install also
/// takes pre-releases. A copy that was not installed (a downloaded exe, <c>dotnet run</c>) never checks.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class UpdateCheck(ILogger<UpdateCheck> logger) : BackgroundService
{
    public const string RepositoryUrl = "https://github.com/MiguelEscSan/LeverDeck";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UpdateManager manager;
        try
        {
            var installed = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            if (!installed.IsInstalled)
            {
                return;
            }

            var prerelease = installed.CurrentVersion?.IsPrerelease ?? false;
            manager = prerelease
                ? new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: true))
                : installed;
        }
        catch (Exception exception)
        {
            LogFailed(exception.Message);
            return;
        }

        try
        {
            await Task.Delay(FirstCheckDelay, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(CheckInterval);
            do
            {
                await CheckAsync(manager, stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The agent is stopping.
        }
    }

    private async Task CheckAsync(UpdateManager manager, CancellationToken cancellationToken)
    {
        try
        {
            if (manager.UpdatePendingRestart is { } pending)
            {
                LogPending(pending.Version);
                return;
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update, progress: null, cancelToken: cancellationToken).ConfigureAwait(false);
            LogDownloaded(update.TargetFullRelease.Version);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Offline, rate-limited by GitHub, or a release without Velopack files: try again at the next check.
            LogFailed(exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "LeverDeck {Version} was downloaded and is installed at the next start.")]
    private partial void LogDownloaded(SemanticVersion version);

    [LoggerMessage(Level = LogLevel.Information, Message = "LeverDeck {Version} is installed at the next start.")]
    private partial void LogPending(SemanticVersion version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check for updates: {Message}")]
    private partial void LogFailed(string message);
}
