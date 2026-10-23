using System.Globalization;
using System.Text;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// The name this PC answers to on the LAN, <c>&lt;label&gt;.local</c> (docs/ARCHITECTURE.md, Connectivity). Each PC has
/// its own, so several agents can share a network and a phone keeps one origin, and one device token, per PC.
/// <see cref="Label"/> is one DNS label (RFC 1123): 1–63 characters of <c>[a-z0-9-]</c>, no leading or trailing dash.
/// </summary>
public sealed record AgentHostName
{
    public const string Prefix = "leverdeck-";

    private const string Domain = ".local";
    private const int MaxLabelLength = 63;

    private AgentHostName(string label) => Label = label;

    public string Label { get; }

    /// <summary>The name devices resolve over mDNS, for example <c>leverdeck-office-pc.local</c>.</summary>
    public string Fqdn => Label + Domain;

    /// <summary>
    /// A readable name from the PC's name: lowercase, without accents, every run of other characters one dash, after
    /// <see cref="Prefix"/> ("DESKTOP-Miguel Ñ" → <c>leverdeck-desktop-miguel-n</c>). When nothing of the machine name
    /// is left, <paramref name="randomSuffix"/> takes its place.
    /// </summary>
    public static AgentHostName FromMachineName(string machineName, Func<string> randomSuffix)
    {
        ArgumentNullException.ThrowIfNull(machineName);
        ArgumentNullException.ThrowIfNull(randomSuffix);

        var cleaned = Clean(machineName);
        var label = Prefix + (cleaned.Length > 0 ? cleaned : Clean(randomSuffix()));
        if (label.Length > MaxLabelLength)
        {
            label = label[..MaxLabelLength].TrimEnd('-');
        }

        return TryParse(label, out var name)
            ? name!
            : throw new InvalidOperationException($"'{label}' is not a valid host name label.");
    }

    /// <summary>Reads a label or a full <c>label.local</c> name, ignoring case; refuses anything that is not one label.</summary>
    public static bool TryParse(string? text, out AgentHostName? name)
    {
        name = null;
        var label = text?.Trim().ToLowerInvariant() ?? string.Empty;
        if (label.EndsWith(Domain, StringComparison.Ordinal))
        {
            label = label[..^Domain.Length];
        }

        if (label.Length is 0 or > MaxLabelLength || label[0] == '-' || label[^1] == '-' || !label.All(IsLabelCharacter))
        {
            return false;
        }

        name = new AgentHostName(label);
        return true;
    }

    public override string ToString() => Fqdn;

    private static bool IsLabelCharacter(char character) => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue; // a combining accent of an already decomposed letter
            }

            var plain = LatinFolds.TryGetValue(character, out var folded) ? folded : character.ToString();
            if (plain.All(IsAsciiLetterOrDigit))
            {
                builder.Append(plain);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private static bool IsAsciiLetterOrDigit(char character) => character is (>= 'a' and <= 'z') or (>= '0' and <= '9');

    /// <summary>
    /// Lowercase Latin letters with accents and their plain spelling. The agent runs with invariant globalization
    /// (Native AOT), where <see cref="string.Normalize()"/> does not decompose them, so the fold is spelled out.
    /// </summary>
    private static readonly Dictionary<char, string> LatinFolds = BuildLatinFolds(
        ("àáâãäåāăą", "a"), ("çćĉċč", "c"), ("ďđ", "d"), ("èéêëēĕėęě", "e"), ("ĝğġģ", "g"), ("ĥħ", "h"),
        ("ìíîïĩīĭįı", "i"), ("ĵ", "j"), ("ķ", "k"), ("ĺļľŀł", "l"), ("ñńņň", "n"), ("òóôõöøōŏő", "o"), ("ŕŗř", "r"),
        ("śŝşš", "s"), ("ţťŧ", "t"), ("ùúûüũūŭůűų", "u"), ("ŵ", "w"), ("ýÿŷ", "y"), ("źżž", "z"), ("ß", "ss"),
        ("æ", "ae"), ("œ", "oe"), ("þ", "th"), ("ð", "d"));

    private static Dictionary<char, string> BuildLatinFolds(params (string Accented, string Plain)[] groups)
    {
        var folds = new Dictionary<char, string>();
        foreach (var (accented, plain) in groups)
        {
            foreach (var character in accented)
            {
                folds[character] = plain;
            }
        }

        return folds;
    }
}
