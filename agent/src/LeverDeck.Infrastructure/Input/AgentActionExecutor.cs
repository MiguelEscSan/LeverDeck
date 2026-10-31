using LeverDeck.Application.Actions.Ports;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.Input;

/// <summary>
/// The agent's <see cref="IActionExecutor"/>: hotkeys and media keys go to <see cref="SendInputActionExecutor"/>;
/// URLs, store game launches and registered apps go to the Windows shell. A registered app is launched by the path
/// stored in the settings, never one from the client. OBS actions go to the OBS client, which fails with
/// <see cref="DomainErrorCode.ActionFailed"/> while OBS is not connected. Client actions are rejected with
/// <see cref="DomainErrorCode.ActionNotExecutable"/>, as a defence in depth.
/// </summary>
public sealed class AgentActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _keys;
    private readonly IShellLauncher _shell;
    private readonly ISettingsRepository _settings;
    private readonly IObsClient _obs;
    private readonly Func<string, bool> _fileExists;

    public AgentActionExecutor(ISettingsRepository settings, IObsClient obs)
        : this(new SendInputActionExecutor(), new WindowsShellLauncher(), settings, obs, File.Exists)
    {
    }

    internal AgentActionExecutor(
        IActionExecutor keys,
        IShellLauncher shell,
        ISettingsRepository settings,
        IObsClient obs,
        Func<string, bool> fileExists)
    {
        _keys = keys;
        _shell = shell;
        _settings = settings;
        _obs = obs;
        _fileExists = fileExists;
    }

    public async Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        return action switch
        {
            Hotkey or MediaKey => await _keys.ExecuteAsync(action, cancellationToken).ConfigureAwait(false),
            OpenUrl open => Shell(_shell.Open(open.Url), "Windows could not open the link."),
            LaunchGame game => Shell(
                _shell.Open(LaunchUris.For(game)),
                $"Windows could not start the game. Is {(game.Store == GameStore.Steam ? "Steam" : "the Epic Games Launcher")} installed?"),
            LaunchApp launch => await LaunchAppAsync(launch, cancellationToken).ConfigureAwait(false),
            ObsScene scene => await _obs.SetSceneAsync(scene.SceneName, cancellationToken).ConfigureAwait(false),
            ObsStreaming streaming => await _obs.ControlStreamAsync(streaming.Command, cancellationToken).ConfigureAwait(false),
            ObsRecord recording => await _obs.ControlRecordingAsync(recording.Command, cancellationToken).ConfigureAwait(false),
            ObsInputMute mute => await _obs.ControlInputMuteAsync(mute.InputName, mute.Command, cancellationToken).ConfigureAwait(false),
            ObsSourceVisibility visibility => await _obs.ControlSourceVisibilityAsync(
                visibility.SceneName,
                visibility.SourceName,
                visibility.Command,
                cancellationToken).ConfigureAwait(false),
            ChangePage or SwitchProfile => Fail(DomainErrorCode.ActionNotExecutable, "This action runs on the client."),
            _ => Fail(DomainErrorCode.ActionNotExecutable, $"{action.GetType().Name} is not supported."),
        };
    }

    private async Task<Result> LaunchAppAsync(LaunchApp launch, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var app = settings.FindApp(launch.App);
        if (app is null)
        {
            return Fail(DomainErrorCode.ActionNotExecutable, $"App {launch.App} is not registered.");
        }

        // A Store app has no file to check: the shell resolves its AppUserModelId when it starts.
        if (!app.IsStoreApp && !_fileExists(app.ExecutablePath))
        {
            return Fail(DomainErrorCode.ActionFailed, $"{app.DisplayName} is no longer at {app.ExecutablePath}.");
        }

        return Shell(_shell.Start(app.ExecutablePath), $"Windows could not start {app.DisplayName}.");
    }

    private static Result Shell(bool accepted, string failure) =>
        accepted ? Result.Success() : Fail(DomainErrorCode.ActionFailed, failure);

    private static Result Fail(DomainErrorCode code, string message) => Result.Failure(new DomainError(code, message));
}
