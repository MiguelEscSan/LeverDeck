namespace LeverDeck.Domain.Actions;

/// <summary>Modifier keys held while a <see cref="Hotkey"/> is pressed.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}
