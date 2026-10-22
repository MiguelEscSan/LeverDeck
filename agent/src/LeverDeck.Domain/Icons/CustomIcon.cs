using System.Text.RegularExpressions;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Icons;

/// <summary>
/// Icons the owner uploads, next to the bundled ones. Their <see cref="IconId"/> is <c>custom-</c> followed by 32
/// lowercase hex digits (a UUID without dashes, chosen by the editor), so a key keeps referring to them by id like any
/// other icon and the id can safely name a file.
/// </summary>
public static partial class CustomIcon
{
    public const string Prefix = "custom-";

    public static bool IsCustom(IconId icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return Format().IsMatch(icon.Value);
    }

    [GeneratedRegex("^custom-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
