using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Domain.Tests.Actions;

public sealed class PlaySoundTests
{
    private static readonly SoundId Clip = new("sound-0123456789abcdef0123456789abcdef");

    [Fact]
    public void Constructor_CarriesOnlyTheSoundIdAndRunsOnAgent()
    {
        var action = new PlaySound(Clip);

        Assert.Equal(Clip, action.Sound);
        Assert.Equal(ExecutionTarget.Agent, action.Target);
    }
}
