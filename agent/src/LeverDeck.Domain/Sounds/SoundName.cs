using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Sounds;

/// <summary>The name the owner gives a clip, shown in the library and on keys: trimmed, 1–60 characters, no control characters.</summary>
public readonly record struct SoundName
{
    public const int MaxLength = 60;

    public SoundName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        Invariant.Require(trimmed.Length is >= 1 and <= MaxLength, DomainErrorCode.ValidationFailed, $"A sound name must be 1–{MaxLength} characters.");
        Invariant.Require(!trimmed.Any(char.IsControl), DomainErrorCode.ValidationFailed, "A sound name must not contain control characters.");
        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
