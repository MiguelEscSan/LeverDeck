using LeverDeck.Domain.Sounds;

namespace LeverDeck.Domain.Actions;

/// <summary>
/// Plays an uploaded sound clip on the PC. It carries only the clip's id: the file is the agent's own, never a path
/// from the client. Pressing the key again restarts the clip.
/// </summary>
public sealed record PlaySound : Action
{
    public PlaySound(SoundId sound)
    {
        Sound = sound;
    }

    public SoundId Sound { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
