using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Obs.Ports;

/// <summary>
/// The agent's link to OBS Studio. Every command fails with <c>actionFailed</c> when OBS is not connected; the client
/// never waits for a reconnect, and it never throws for an OBS-side refusal.
/// </summary>
public interface IObsClient
{
    /// <summary>True while the socket is open and OBS has accepted the password.</summary>
    bool IsConnected { get; }

    Task<Result<ObsSceneList>> GetScenesAsync(CancellationToken cancellationToken);

    Task<Result> SetSceneAsync(string sceneName, CancellationToken cancellationToken);

    Task<Result> ControlStreamAsync(ObsOutputCommand command, CancellationToken cancellationToken);

    Task<Result> ControlRecordingAsync(ObsOutputCommand command, CancellationToken cancellationToken);

    Task<Result> ControlInputMuteAsync(string inputName, ObsMuteCommand command, CancellationToken cancellationToken);

    Task<Result> ControlSourceVisibilityAsync(
        string sceneName,
        string sourceName,
        ObsVisibilityCommand command,
        CancellationToken cancellationToken);
}
