using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Devices;

/// <summary>
/// Opaque hash of a device token, at most 256 characters. The raw token is never stored; how it is hashed and
/// encoded is an Infrastructure concern, so the value is kept verbatim.
/// </summary>
public sealed record TokenHash
{
    public const int MaxLength = 256;

    public TokenHash(string value)
    {
        Invariant.Require(
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"Token hash must be non-empty and at most {MaxLength} characters.");
        Value = value;
    }

    public string Value { get; }

    /// <summary>Redacted so the hash never ends up in logs.</summary>
    public override string ToString() => "TokenHash(***)";
}
