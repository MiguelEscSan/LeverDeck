using System.Text.RegularExpressions;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>
/// Identifier of a bundled icon in lowercase kebab-case (for example <c>volume-up</c>).
/// Only the format is validated until the bundled icon set exists.
/// </summary>
public sealed partial record IconId
{
    public const int MaxLength = 64;

    public IconId(string value)
    {
        Invariant.Require(
            value is { Length: > 0 and <= MaxLength } && KebabCase().IsMatch(value),
            DomainErrorCode.ValidationFailed,
            $"Icon id must be lowercase kebab-case, 1–{MaxLength} characters.");
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KebabCase();
}
