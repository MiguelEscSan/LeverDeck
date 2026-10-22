using LeverDeck.Application.Common;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Settings;

/// <summary>Query (admin scope): the agent settings.</summary>
public sealed class GetSettings(ISettingsRepository settings)
{
    public Task<Result<AgentSettings>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
            Result.Success(await settings.GetAsync(cancellationToken).ConfigureAwait(false)));
}
