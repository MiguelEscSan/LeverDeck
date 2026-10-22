using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Ports;

/// <summary>Loads and saves the agent settings. When nothing is stored yet, the adapter returns the defaults.</summary>
public interface ISettingsRepository
{
    Task<AgentSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken);
}
