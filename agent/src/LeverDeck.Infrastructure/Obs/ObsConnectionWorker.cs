using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Obs;

/// <summary>
/// Keeps the OBS session in line with the stored settings: every few seconds it reconnects a dropped link and closes
/// a session whose connection was changed. Failures are not fatal; the next tick tries again.
/// </summary>
internal sealed partial class ObsConnectionWorker(ObsWebSocketClient client, ILogger<ObsConnectionWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await client.MaintainAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogMaintainFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The OBS connection check failed; the next check will retry.")]
    private static partial void LogMaintainFailed(ILogger logger, Exception exception);
}
