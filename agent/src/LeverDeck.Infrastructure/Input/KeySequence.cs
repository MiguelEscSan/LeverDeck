using LeverDeck.Domain.Actions;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LeverDeck.Infrastructure.Input;

/// <summary>One key press or release to inject.</summary>
internal readonly record struct KeyStroke(VIRTUAL_KEY Key, bool IsKeyUp, bool IsExtended);

/// <summary>Builds the keystrokes for an action without touching the OS, so the order is unit-testable.</summary>
internal static class KeySequence
{
    // Pressed in this order and released in reverse.
    private static readonly KeyModifiers[] ModifierOrder = [KeyModifiers.Ctrl, KeyModifiers.Shift, KeyModifiers.Alt, KeyModifiers.Win];

    /// <summary>Modifiers down, key down, key up, modifiers up.</summary>
    public static IReadOnlyList<KeyStroke> For(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        var modifiers = ModifierOrder.Where(modifier => hotkey.Modifiers.HasFlag(modifier)).Select(KeyCodes.For).ToList();
        var key = KeyCodes.For(hotkey.Key);

        List<KeyStroke> strokes = [.. modifiers.Select(modifier => Stroke(modifier, isKeyUp: false))];
        strokes.Add(Stroke(key, isKeyUp: false));
        strokes.Add(Stroke(key, isKeyUp: true));
        strokes.AddRange(Enumerable.Reverse(modifiers).Select(modifier => Stroke(modifier, isKeyUp: true)));
        return strokes;
    }

    public static IReadOnlyList<KeyStroke> For(MediaKey mediaKey)
    {
        ArgumentNullException.ThrowIfNull(mediaKey);
        var key = KeyCodes.For(mediaKey.Key);
        return [Stroke(key, isKeyUp: false), Stroke(key, isKeyUp: true)];
    }

    private static KeyStroke Stroke(VIRTUAL_KEY key, bool isKeyUp) => new(key, isKeyUp, KeyCodes.IsExtended(key));
}
