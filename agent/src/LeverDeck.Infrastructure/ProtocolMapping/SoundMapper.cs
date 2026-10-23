using LeverDeck.Domain.Common;
using LeverDeck.Domain.Sounds;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Maps sound clips to their wire form and reads the sound fields of the requests.</summary>
public static class SoundMapper
{
    public static SoundDto ToDto(SoundClip clip) => new(clip.Id.Value, clip.Name.Value, clip.DurationMs, clip.SizeBytes);

    public static Result<SoundId> ToSoundId(string? value) => MappingBoundary.Run(() => new SoundId(value ?? string.Empty));

    public static Result<SoundName> ToSoundName(string? value) => MappingBoundary.Run(() => new SoundName(value ?? string.Empty));
}
