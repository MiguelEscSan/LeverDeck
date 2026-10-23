using System.Text.RegularExpressions;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Sounds;

/// <summary>
/// The id of an uploaded sound clip: <c>sound-</c> followed by 32 lowercase hex digits, chosen by the editor (a UUID
/// without dashes). The id names the clip's file, so only this exact shape is accepted; a key keeps referring to the
/// clip by id, and a rename never changes it.
/// </summary>
public readonly partial record struct SoundId
{
    public const string Prefix = "sound-";

    public SoundId(string value)
    {
        Invariant.Require(value is not null && Format().IsMatch(value), DomainErrorCode.ValidationFailed, "A sound id must be 'sound-' and 32 lowercase hex digits.");
        Value = value!;
    }

    public string Value { get; }

    /// <summary>The id, or <see langword="null"/> when the text is not one (for example a file name read from disk).</summary>
    public static SoundId? TryParse(string? value) => value is not null && Format().IsMatch(value) ? new SoundId(value) : null;

    public override string ToString() => Value;

    [GeneratedRegex("^sound-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
