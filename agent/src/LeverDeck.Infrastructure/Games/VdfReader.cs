using System.Text;

namespace LeverDeck.Infrastructure.Games;

/// <summary>
/// A section of a Valve KeyValues (VDF) text file: string values and nested sections, keys compared ignoring case.
/// A repeated key keeps its first value.
/// </summary>
internal sealed class VdfSection
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VdfSection> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VdfSection> _orderedSections = [];

    public IReadOnlyList<VdfSection> Sections => _orderedSections;

    public string? Value(string key) => _values.GetValueOrDefault(key);

    public VdfSection? Section(string key) => _sections.GetValueOrDefault(key);

    internal void Add(string key, string value) => _values.TryAdd(key, value);

    internal void Add(string key, VdfSection section)
    {
        if (_sections.TryAdd(key, section))
        {
            _orderedSections.Add(section);
        }
    }
}

/// <summary>
/// Reads the KeyValues text format Steam uses for <c>libraryfolders.vdf</c> and <c>appmanifest_*.acf</c>: quoted or
/// bare tokens, <c>{ }</c> sections, <c>//</c> comments and backslash escapes. Malformed input yields <c>null</c>.
/// </summary>
internal static class VdfReader
{
    public static VdfSection? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var position = 0;
        var root = new VdfSection();
        return ReadSection(text, ref position, root, nested: false) ? root : null;
    }

    private static bool ReadSection(string text, ref int position, VdfSection section, bool nested)
    {
        while (true)
        {
            var key = NextToken(text, ref position);
            if (key is null)
            {
                return !nested;
            }

            if (key.Kind == TokenKind.Close)
            {
                return nested;
            }

            if (key.Kind != TokenKind.Text)
            {
                return false;
            }

            var value = NextToken(text, ref position);
            switch (value?.Kind)
            {
                case TokenKind.Text:
                    section.Add(key.Text, value.Text);
                    break;
                case TokenKind.Open:
                    var child = new VdfSection();
                    if (!ReadSection(text, ref position, child, nested: true))
                    {
                        return false;
                    }

                    section.Add(key.Text, child);
                    break;
                default:
                    return false;
            }
        }
    }

    private enum TokenKind
    {
        Text,
        Open,
        Close,
    }

    private sealed record Token(TokenKind Kind, string Text);

    private static Token? NextToken(string text, ref int position)
    {
        while (position < text.Length)
        {
            var c = text[position];
            if (char.IsWhiteSpace(c))
            {
                position++;
            }
            else if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                while (position < text.Length && text[position] != '\n')
                {
                    position++;
                }
            }
            else
            {
                break;
            }
        }

        if (position >= text.Length)
        {
            return null;
        }

        switch (text[position])
        {
            case '{':
                position++;
                return new Token(TokenKind.Open, "{");
            case '}':
                position++;
                return new Token(TokenKind.Close, "}");
            case '"':
                return ReadQuoted(text, ref position);
            default:
                var start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('{' or '}' or '"'))
                {
                    position++;
                }

                return new Token(TokenKind.Text, text[start..position]);
        }
    }

    private static Token? ReadQuoted(string text, ref int position)
    {
        position++;
        var builder = new StringBuilder();
        while (position < text.Length)
        {
            var c = text[position++];
            if (c == '"')
            {
                return new Token(TokenKind.Text, builder.ToString());
            }

            if (c == '\\' && position < text.Length)
            {
                var escaped = text[position++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => escaped,
                });
            }
            else
            {
                builder.Append(c);
            }
        }

        // Unterminated string.
        return null;
    }
}
