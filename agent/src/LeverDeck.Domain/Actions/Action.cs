namespace LeverDeck.Domain.Actions;

/// <summary>
/// What a button does. The hierarchy is closed: only the sealed records in this assembly derive from it, and
/// <see cref="ActionCatalog"/> lists them. No action can run an arbitrary command.
/// </summary>
public abstract record Action
{
    private protected Action()
    {
    }

    /// <summary>Where the action runs: on the PC agent or on the client device.</summary>
    public ExecutionTarget Target => RunsOn;

    // Not overridable outside this assembly, so the protected record copy constructor cannot be used to derive from Action.
    private protected abstract ExecutionTarget RunsOn { get; }
}
