using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Shows, hides or toggles a source inside an OBS scene; both names are trimmed, 1–100 characters.</summary>
public sealed record ObsSourceVisibility : Action
{
    public ObsSourceVisibility(string sceneName, string sourceName, ObsVisibilityCommand command)
    {
        Invariant.RequireDefined(command, "visibility command");
        SceneName = ObsName.Required(sceneName, "Scene name");
        SourceName = ObsName.Required(sourceName, "Source name");
        Command = command;
    }

    public string SceneName { get; }

    public string SourceName { get; }

    public ObsVisibilityCommand Command { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
