using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds.Ports;

/// <summary>Where uploaded sound clips live: the clip list and the WAV file of each clip, by id.</summary>
public interface ISoundStore
{
    Task<IReadOnlyList<SoundClip>> ListAsync(CancellationToken cancellationToken);

    Task<SoundClip?> FindAsync(SoundId sound, CancellationToken cancellationToken);

    /// <summary>Stores a new clip and its WAV file together.</summary>
    Task SaveAsync(SoundClip clip, ReadOnlyMemory<byte> wave, CancellationToken cancellationToken);

    /// <summary>Changes the name of a stored clip; its id and file stay as they are.</summary>
    Task RenameAsync(SoundId sound, SoundName name, CancellationToken cancellationToken);

    Task DeleteAsync(SoundId sound, CancellationToken cancellationToken);
}
