using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Starts, stops or toggles the OBS recording.</summary>
public sealed record ObsRecord : Action
{
    public ObsRecord(ObsOutputCommand command)
    {
        Invariant.RequireDefined(command, "recording command");
        Command = command;
    }

    public ObsOutputCommand Command { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
