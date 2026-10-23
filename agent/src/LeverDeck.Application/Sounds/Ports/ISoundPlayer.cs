using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds.Ports;

/// <summary>Plays a stored clip on the PC's speakers. Playing a clip that already plays restarts it.</summary>
public interface ISoundPlayer
{
    /// <summary>Starts the clip; <see langword="false"/> when the OS refuses or the file is missing.</summary>
    bool Play(SoundId sound);
}
