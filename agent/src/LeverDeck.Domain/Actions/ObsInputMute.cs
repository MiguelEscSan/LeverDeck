using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Mutes, unmutes or toggles an OBS audio input by its trimmed name, 1–100 characters.</summary>
public sealed record ObsInputMute : Action
{
    public ObsInputMute(string inputName, ObsMuteCommand command)
    {
        Invariant.RequireDefined(command, "mute command");
        InputName = ObsName.Required(inputName, "Input name");
        Command = command;
    }

    public string InputName { get; }

    public ObsMuteCommand Command { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
