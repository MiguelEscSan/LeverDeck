using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Devices;

/// <summary>Trimmed device name, 1–40 characters.</summary>
public sealed record DeviceName
{
    public const int MaxLength = 40;

    public DeviceName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmed.Length is >= 1 and <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"Device name must be 1–{MaxLength} characters.");
        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
