using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemorySettingsRepository : ISettingsRepository
{
    public AgentSettings Stored { get; set; } = AgentSettings.CreateDefault();

    public int Writes { get; private set; }

    public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken)
    {
        Writes++;
        Stored = settings;
        return Task.CompletedTask;
    }
}
