using LeverDeck.Application.Obs;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Tests.Fakes;

/// <summary>Records every OBS command and answers with the configured results.</summary>
internal sealed class FakeObsClient : IObsClient
{
    private readonly List<string> _commands = [];

    public bool IsConnected { get; set; } = true;

    public Result<ObsSceneList> Scenes { get; set; } = Result.Success(new ObsSceneList(["Gameplay", "Starting soon"], "Gameplay"));

    public Result NextResult { get; set; } = Result.Success();

    /// <summary>One entry per command, for example <c>stream:Toggle</c>.</summary>
    public IReadOnlyList<string> Commands => _commands;

    public Task<Result<ObsSceneList>> GetScenesAsync(CancellationToken cancellationToken)
    {
        _commands.Add("scenes");
        return Task.FromResult(IsConnected ? Scenes : Result.Failure<ObsSceneList>(NotConnected));
    }

    public Task<Result> SetSceneAsync(string sceneName, CancellationToken cancellationToken)
    {
        _commands.Add($"scene:{sceneName}");
        return Task.FromResult(Answer());
    }

    public Task<Result> ControlStreamAsync(ObsOutputCommand command, CancellationToken cancellationToken)
    {
        _commands.Add($"stream:{command}");
        return Task.FromResult(Answer());
    }

    public Task<Result> ControlRecordingAsync(ObsOutputCommand command, CancellationToken cancellationToken)
    {
        _commands.Add($"record:{command}");
        return Task.FromResult(Answer());
    }

    public Task<Result> ControlInputMuteAsync(string inputName, ObsMuteCommand command, CancellationToken cancellationToken)
    {
        _commands.Add($"mute:{inputName}:{command}");
        return Task.FromResult(Answer());
    }

    public Task<Result> ControlSourceVisibilityAsync(
        string sceneName,
        string sourceName,
        ObsVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        _commands.Add($"visibility:{sceneName}:{sourceName}:{command}");
        return Task.FromResult(Answer());
    }

    private Result Answer() => IsConnected ? NextResult : Result.Failure(NotConnected);

    private static DomainError NotConnected => new(DomainErrorCode.ActionFailed, "OBS is not connected.");
}
