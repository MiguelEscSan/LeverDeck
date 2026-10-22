using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Pairing;

/// <summary>Exactly 6 ASCII digits (0–9). Other Unicode digits, whitespace and signs are rejected.</summary>
public sealed record PairingCode
{
    public const int Length = 6;

    public PairingCode(string value)
    {
        Invariant.Require(
            value is { Length: Length } && value.All(char.IsAsciiDigit),
            DomainErrorCode.ValidationFailed,
            $"Pairing code must be exactly {Length} digits.");
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
