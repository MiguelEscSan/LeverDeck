namespace LeverDeck.Domain.Actions;

/// <summary>Where an action is executed.</summary>
public enum ExecutionTarget
{
    /// <summary>On the PC, by the agent.</summary>
    Agent,

    /// <summary>On the phone or tablet, without reaching the agent's executors.</summary>
    Client,
}
