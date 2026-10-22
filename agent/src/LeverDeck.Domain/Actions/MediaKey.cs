using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Presses a media or volume key.</summary>
public sealed record MediaKey : Action
{
    public MediaKey(MediaKeyKind key)
    {
        Invariant.RequireDefined(key, "media key");
        Key = key;
    }

    public MediaKeyKind Key { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
