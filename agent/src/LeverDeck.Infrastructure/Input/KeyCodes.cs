using LeverDeck.Domain.Actions;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LeverDeck.Infrastructure.Input;

/// <summary>Pure tables from the domain key allow-lists to Windows virtual-key codes.</summary>
internal static class KeyCodes
{
    public static VIRTUAL_KEY For(VirtualKey key) => key switch
    {
        >= VirtualKey.A and <= VirtualKey.Z => VIRTUAL_KEY.VK_A + (ushort)(key - VirtualKey.A),
        >= VirtualKey.D0 and <= VirtualKey.D9 => VIRTUAL_KEY.VK_0 + (ushort)(key - VirtualKey.D0),
        >= VirtualKey.F1 and <= VirtualKey.F24 => VIRTUAL_KEY.VK_F1 + (ushort)(key - VirtualKey.F1),
        VirtualKey.Up => VIRTUAL_KEY.VK_UP,
        VirtualKey.Down => VIRTUAL_KEY.VK_DOWN,
        VirtualKey.Left => VIRTUAL_KEY.VK_LEFT,
        VirtualKey.Right => VIRTUAL_KEY.VK_RIGHT,
        VirtualKey.Enter => VIRTUAL_KEY.VK_RETURN,
        VirtualKey.Escape => VIRTUAL_KEY.VK_ESCAPE,
        VirtualKey.Space => VIRTUAL_KEY.VK_SPACE,
        VirtualKey.Tab => VIRTUAL_KEY.VK_TAB,
        VirtualKey.Backspace => VIRTUAL_KEY.VK_BACK,
        VirtualKey.Delete => VIRTUAL_KEY.VK_DELETE,
        VirtualKey.Insert => VIRTUAL_KEY.VK_INSERT,
        VirtualKey.Home => VIRTUAL_KEY.VK_HOME,
        VirtualKey.End => VIRTUAL_KEY.VK_END,
        VirtualKey.PageUp => VIRTUAL_KEY.VK_PRIOR,
        VirtualKey.PageDown => VIRTUAL_KEY.VK_NEXT,
        VirtualKey.PrintScreen => VIRTUAL_KEY.VK_SNAPSHOT,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Key is not in the allow-list."),
    };

    public static VIRTUAL_KEY For(MediaKeyKind key) => key switch
    {
        MediaKeyKind.PlayPause => VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE,
        MediaKeyKind.Next => VIRTUAL_KEY.VK_MEDIA_NEXT_TRACK,
        MediaKeyKind.Previous => VIRTUAL_KEY.VK_MEDIA_PREV_TRACK,
        MediaKeyKind.Stop => VIRTUAL_KEY.VK_MEDIA_STOP,
        MediaKeyKind.VolumeUp => VIRTUAL_KEY.VK_VOLUME_UP,
        MediaKeyKind.VolumeDown => VIRTUAL_KEY.VK_VOLUME_DOWN,
        MediaKeyKind.Mute => VIRTUAL_KEY.VK_VOLUME_MUTE,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown media key."),
    };

    public static VIRTUAL_KEY For(KeyModifiers modifier) => modifier switch
    {
        KeyModifiers.Ctrl => VIRTUAL_KEY.VK_LCONTROL,
        KeyModifiers.Shift => VIRTUAL_KEY.VK_LSHIFT,
        KeyModifiers.Alt => VIRTUAL_KEY.VK_LMENU,
        KeyModifiers.Win => VIRTUAL_KEY.VK_LWIN,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Expected a single modifier."),
    };

    /// <summary>
    /// Keys outside the main block need <c>KEYEVENTF_EXTENDEDKEY</c>, or Windows reads them as their numeric-keypad
    /// twins (for example Up as keypad 8).
    /// </summary>
    public static bool IsExtended(VIRTUAL_KEY key) => key is
        VIRTUAL_KEY.VK_UP or VIRTUAL_KEY.VK_DOWN or VIRTUAL_KEY.VK_LEFT or VIRTUAL_KEY.VK_RIGHT
        or VIRTUAL_KEY.VK_INSERT or VIRTUAL_KEY.VK_DELETE or VIRTUAL_KEY.VK_HOME or VIRTUAL_KEY.VK_END
        or VIRTUAL_KEY.VK_PRIOR or VIRTUAL_KEY.VK_NEXT or VIRTUAL_KEY.VK_SNAPSHOT
        or VIRTUAL_KEY.VK_LWIN
        or VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE or VIRTUAL_KEY.VK_MEDIA_NEXT_TRACK or VIRTUAL_KEY.VK_MEDIA_PREV_TRACK
        or VIRTUAL_KEY.VK_MEDIA_STOP or VIRTUAL_KEY.VK_VOLUME_UP or VIRTUAL_KEY.VK_VOLUME_DOWN or VIRTUAL_KEY.VK_VOLUME_MUTE;
}
