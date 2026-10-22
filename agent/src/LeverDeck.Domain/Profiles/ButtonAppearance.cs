using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>What a key looks like: a label of 0–12 characters, a palette color and an icon.</summary>
public sealed record ButtonAppearance
{
    public const int MaxLabelLength = 12;

    public ButtonAppearance(string label, KeyColor color, IconId icon)
    {
        Invariant.Require(
            label is { Length: <= MaxLabelLength },
            DomainErrorCode.ValidationFailed,
            $"Label must be at most {MaxLabelLength} characters.");
        Invariant.RequireDefined(color, "key color");
        ArgumentNullException.ThrowIfNull(icon);

        Label = label;
        Color = color;
        Icon = icon;
    }

    public string Label { get; }

    public KeyColor Color { get; }

    public IconId Icon { get; }
}
