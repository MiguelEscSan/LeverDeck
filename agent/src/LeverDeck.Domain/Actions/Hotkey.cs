using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Presses a key from the <see cref="VirtualKey"/> allow-list while holding the given modifiers.</summary>
public sealed record Hotkey : Action
{
    private const KeyModifiers AllModifiers = KeyModifiers.Ctrl | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Win;

    public Hotkey(KeyModifiers modifiers, VirtualKey key)
    {
        Invariant.Require(
            (modifiers & ~AllModifiers) == KeyModifiers.None,
            DomainErrorCode.ValidationFailed,
            $"Unknown key modifiers '{modifiers}'.");
        Invariant.RequireDefined(key, "virtual key");

        Modifiers = modifiers;
        Key = key;
    }

    public KeyModifiers Modifiers { get; }

    public VirtualKey Key { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
