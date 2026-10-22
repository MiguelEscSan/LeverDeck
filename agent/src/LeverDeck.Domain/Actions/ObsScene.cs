using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Switches OBS to a scene by its trimmed name, 1–100 characters.</summary>
public sealed record ObsScene : Action
{
    public const int MaxLength = 100;

    public ObsScene(string sceneName)
    {
        var trimmed = sceneName?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmed.Length is >= 1 and <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"Scene name must be 1–{MaxLength} characters.");
        SceneName = trimmed;
    }

    public string SceneName { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
