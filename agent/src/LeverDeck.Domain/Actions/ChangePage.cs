namespace LeverDeck.Domain.Actions;

/// <summary>Moves the client to another page of the active profile.</summary>
public sealed record ChangePage : Action
{
    public ChangePage(PageTarget destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Destination = destination;
    }

    public PageTarget Destination { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Client;
}
