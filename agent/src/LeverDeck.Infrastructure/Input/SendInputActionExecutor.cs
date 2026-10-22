using LeverDeck.Application.Actions.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.Input;

/// <summary>
/// Runs hotkeys and media keys through <c>SendInput</c>. The other agent actions arrive in Phase 7 and fail with
/// <see cref="DomainErrorCode.ActionNotExecutable"/> until then; client actions are rejected the same way, as a
/// defence in depth (<c>ExecuteAction</c> never routes them here). Presses are serialized, so the modifiers of two
/// concurrent hotkeys never mix.
/// </summary>
public sealed class SendInputActionExecutor : IActionExecutor
{
    private readonly IKeyboardInput _keyboard;
    private readonly Lock _gate = new();

    public SendInputActionExecutor()
        : this(new Win32KeyboardInput())
    {
    }

    internal SendInputActionExecutor(IKeyboardInput keyboard)
    {
        _keyboard = keyboard;
    }

    public Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        var result = action switch
        {
            Hotkey hotkey => Press(KeySequence.For(hotkey)),
            MediaKey mediaKey => Press(KeySequence.For(mediaKey)),
            LaunchApp or OpenUrl or ObsScene or LaunchGame => Fail(
                DomainErrorCode.ActionNotExecutable,
                $"{action.GetType().Name} actions arrive in Phase 7."),
            ChangePage or SwitchProfile => Fail(DomainErrorCode.ActionNotExecutable, "This action runs on the client."),
            _ => Fail(DomainErrorCode.ActionNotExecutable, $"{action.GetType().Name} is not supported."),
        };
        return Task.FromResult(result);
    }

    private Result Press(IReadOnlyList<KeyStroke> strokes)
    {
        bool accepted;
        lock (_gate)
        {
            accepted = _keyboard.Send(strokes);
        }

        return accepted
            ? Result.Success()
            : Fail(DomainErrorCode.ActionFailed, "Windows rejected the input (an elevated window may have focus).");
    }

    private static Result Fail(DomainErrorCode code, string message) => Result.Failure(new DomainError(code, message));
}
