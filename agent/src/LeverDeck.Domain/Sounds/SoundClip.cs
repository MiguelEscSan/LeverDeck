using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Sounds;

/// <summary>An uploaded sound clip as the library lists it: its id, its name and what its file holds.</summary>
public sealed record SoundClip
{
    public SoundClip(SoundId id, SoundName name, int durationMs, long sizeBytes)
    {
        Invariant.Require(durationMs >= 0, DomainErrorCode.ValidationFailed, "A sound's duration must not be negative.");
        Invariant.Require(sizeBytes is > 0 and <= WaveFile.MaxBytes, DomainErrorCode.ValidationFailed, "A sound's size is out of range.");
        Id = id;
        Name = name;
        DurationMs = durationMs;
        SizeBytes = sizeBytes;
    }

    public SoundId Id { get; }

    public SoundName Name { get; }

    public int DurationMs { get; }

    public long SizeBytes { get; }

    /// <summary>The same clip under another name; its id and file stay as they are.</summary>
    public SoundClip Renamed(SoundName name) => new(Id, name, DurationMs, SizeBytes);
}
