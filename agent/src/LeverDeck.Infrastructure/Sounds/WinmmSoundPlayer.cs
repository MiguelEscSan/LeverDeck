using System.Runtime.Versioning;
using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Sounds;
using Windows.Win32;
using Windows.Win32.Media.Audio;

namespace LeverDeck.Infrastructure.Sounds;

/// <summary>
/// Plays a clip with <c>PlaySound</c> (winmm), asynchronously: the call returns at once and the sound plays on its own.
/// Calling it again with the same file stops the clip and starts it from the beginning, which is the key's restart.
/// Uncompressed WAV only (the domain checks it), and no administrator rights are needed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinmmSoundPlayer(FileSoundStore store) : ISoundPlayer
{
    public bool Play(SoundId sound)
    {
        var path = store.PathOf(sound);
        return File.Exists(path)
            && PInvoke.PlaySound(path, default, SND_FLAGS.SND_FILENAME | SND_FLAGS.SND_ASYNC | SND_FLAGS.SND_NODEFAULT);
    }
}
