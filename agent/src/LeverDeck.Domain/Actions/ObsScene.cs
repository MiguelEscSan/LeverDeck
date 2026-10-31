namespace LeverDeck.Domain.Actions;

/// <summary>Switches OBS to a scene by its trimmed name, 1–100 characters.</summary>
public sealed record ObsScene : Action
{
    public const int MaxLength = ObsName.MaxLength;

    public ObsScene(string sceneName)
    {
        SceneName = ObsName.Required(sceneName, "Scene name");
    }

    public string SceneName { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
