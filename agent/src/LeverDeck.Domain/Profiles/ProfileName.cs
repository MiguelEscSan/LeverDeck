using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>Trimmed profile name, 1–40 characters. Uniqueness across profiles is enforced by use cases.</summary>
public sealed record ProfileName
{
    public const int MaxLength = 40;

    public ProfileName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmed.Length is >= 1 and <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"Profile name must be 1–{MaxLength} characters.");
        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
