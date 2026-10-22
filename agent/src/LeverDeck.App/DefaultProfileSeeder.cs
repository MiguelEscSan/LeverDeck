using LeverDeck.Application.Profiles.Ports;

namespace LeverDeck.App;

/// <summary>
/// Reads the profiles once on start, so a first run writes the seeded "Default" profile to disk right away instead of
/// on the first request. The owner sees a usable profile (and its ids) before any device or editor connects.
/// </summary>
internal sealed class DefaultProfileSeeder(IProfileRepository profiles) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) =>
        _ = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
