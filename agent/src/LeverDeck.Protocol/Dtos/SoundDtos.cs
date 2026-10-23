namespace LeverDeck.Protocol.Dtos;

/// <summary>An uploaded sound clip as the library lists it. The WAV file itself is never sent over the socket.</summary>
public sealed record SoundDto(string Id, string Name, int DurationMs, long SizeBytes);
