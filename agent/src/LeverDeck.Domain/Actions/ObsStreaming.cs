using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Starts, stops or toggles the OBS stream.</summary>
public sealed record ObsStreaming : Action
{
    public ObsStreaming(ObsOutputCommand command)
    {
        Invariant.RequireDefined(command, "stream command");
        Command = command;
    }

    public ObsOutputCommand Command { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
