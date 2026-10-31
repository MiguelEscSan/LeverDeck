using Microsoft.Extensions.Hosting;

namespace LeverDeck.Infrastructure.Obs;

/// <summary>
/// Keeps the OBS session in line with the stored settings: every few seconds it reconnects a dropped link and closes
/// a session whose connection was changed. Failures are not fatal; the next tick tries again.
/// </summary>
internal sealed class ObsConnectionWorker(ObsWebSocketClient client) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await client.MaintainAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
