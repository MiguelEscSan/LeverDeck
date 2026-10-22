using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Domain.Profiles;

/// <summary>A key on a page with what it looks like and what it does. Changes go through <see cref="Profile"/>.</summary>
public sealed class Button
{
    public Button(ButtonId id, GridPosition position, ButtonAppearance appearance, DomainAction action)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(action);
        Id = id;
        Position = position;
        Appearance = appearance;
        Action = action;
    }

    public ButtonId Id { get; }

    public GridPosition Position { get; internal set; }

    public ButtonAppearance Appearance { get; internal set; }

    public DomainAction Action { get; internal set; }
}
