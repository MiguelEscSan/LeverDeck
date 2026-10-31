using LeverDeck.Application.Common;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Obs;

/// <summary>Query (device scope): the scenes OBS has, for the scene picker. It changes nothing in OBS.</summary>
public sealed class GetObsScenes(IObsClient obs)
{
    public Task<Result<ObsSceneList>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, () => obs.GetScenesAsync(cancellationToken));
}
