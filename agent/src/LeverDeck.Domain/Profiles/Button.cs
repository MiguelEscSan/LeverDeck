namespace LeverDeck.Domain.Profiles;

/// <summary>A key on a page. Its action is added in P2-2; position changes go through <see cref="Profile"/>.</summary>
public sealed class Button
{
    public Button(ButtonId id, GridPosition position, ButtonAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        Id = id;
        Position = position;
        Appearance = appearance;
    }

    public ButtonId Id { get; }

    public GridPosition Position { get; internal set; }

    public ButtonAppearance Appearance { get; internal set; }
}
