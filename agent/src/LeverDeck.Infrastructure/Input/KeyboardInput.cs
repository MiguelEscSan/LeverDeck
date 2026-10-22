using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LeverDeck.Infrastructure.Input;

/// <summary>Injects keystrokes into the OS. Tests substitute a recording fake, so they never inject input.</summary>
internal interface IKeyboardInput
{
    /// <summary>Injects every stroke in one batch; <see langword="false"/> if Windows did not accept all of them.</summary>
    bool Send(IReadOnlyList<KeyStroke> strokes);
}

/// <summary>
/// <c>SendInput</c>: the strokes go in as one batch, so they are not interleaved with other input. Windows blocks it
/// (UIPI) when the foreground window belongs to a process with a higher integrity level, for example an elevated app.
/// </summary>
internal sealed class Win32KeyboardInput : IKeyboardInput
{
    public unsafe bool Send(IReadOnlyList<KeyStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        Span<INPUT> inputs = stackalloc INPUT[strokes.Count];
        for (var index = 0; index < strokes.Count; index++)
        {
            var stroke = strokes[index];
            inputs[index].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[index].Anonymous.ki = new KEYBDINPUT
            {
                wVk = stroke.Key,
                dwFlags = (stroke.IsKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0)
                    | (stroke.IsExtended ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0),
            };
        }

        return PInvoke.SendInput(inputs, sizeof(INPUT)) == strokes.Count;
    }
}
