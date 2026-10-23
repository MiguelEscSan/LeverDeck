using LeverDeck.Domain.Actions;
using LeverDeck.Infrastructure.Input;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LeverDeck.Infrastructure.Tests.Input;

public sealed class KeyCodesTests
{
    [Fact]
    public void EveryVirtualKey_MapsToADistinctCode()
    {
        var codes = Enum.GetValues<VirtualKey>().Select(KeyCodes.For).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.DoesNotContain((VIRTUAL_KEY)0, codes);
    }

    [Fact]
    public void EveryMediaKey_MapsToADistinctCode()
    {
        var codes = Enum.GetValues<MediaKeyKind>().Select(KeyCodes.For).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.DoesNotContain((VIRTUAL_KEY)0, codes);
    }

    [Theory]
    [InlineData(VirtualKey.A, 0x41)]
    [InlineData(VirtualKey.Z, 0x5A)]
    [InlineData(VirtualKey.D0, 0x30)]
    [InlineData(VirtualKey.D9, 0x39)]
    [InlineData(VirtualKey.F1, 0x70)]
    [InlineData(VirtualKey.F12, 0x7B)]
    [InlineData(VirtualKey.F24, 0x87)]
    [InlineData(VirtualKey.Up, 0x26)]
    [InlineData(VirtualKey.Down, 0x28)]
    [InlineData(VirtualKey.Left, 0x25)]
    [InlineData(VirtualKey.Right, 0x27)]
    [InlineData(VirtualKey.Enter, 0x0D)]
    [InlineData(VirtualKey.Escape, 0x1B)]
    [InlineData(VirtualKey.Space, 0x20)]
    [InlineData(VirtualKey.Tab, 0x09)]
    [InlineData(VirtualKey.Backspace, 0x08)]
    [InlineData(VirtualKey.Delete, 0x2E)]
    [InlineData(VirtualKey.Insert, 0x2D)]
    [InlineData(VirtualKey.Home, 0x24)]
    [InlineData(VirtualKey.End, 0x23)]
    [InlineData(VirtualKey.PageUp, 0x21)]
    [InlineData(VirtualKey.PageDown, 0x22)]
    [InlineData(VirtualKey.PrintScreen, 0x2C)]
    [InlineData(VirtualKey.NumPad0, 0x60)]
    [InlineData(VirtualKey.NumPad5, 0x65)]
    [InlineData(VirtualKey.NumPad9, 0x69)]
    [InlineData(VirtualKey.NumPadMultiply, 0x6A)]
    [InlineData(VirtualKey.NumPadAdd, 0x6B)]
    [InlineData(VirtualKey.NumPadSubtract, 0x6D)]
    [InlineData(VirtualKey.NumPadDecimal, 0x6E)]
    [InlineData(VirtualKey.NumPadDivide, 0x6F)]
    [InlineData(VirtualKey.NumLock, 0x90)]
    public void VirtualKey_MapsToTheWindowsCode(VirtualKey key, int expected)
    {
        Assert.Equal(expected, (int)KeyCodes.For(key));
    }

    [Theory]
    [InlineData(MediaKeyKind.PlayPause, 0xB3)]
    [InlineData(MediaKeyKind.Next, 0xB0)]
    [InlineData(MediaKeyKind.Previous, 0xB1)]
    [InlineData(MediaKeyKind.Stop, 0xB2)]
    [InlineData(MediaKeyKind.VolumeUp, 0xAF)]
    [InlineData(MediaKeyKind.VolumeDown, 0xAE)]
    [InlineData(MediaKeyKind.Mute, 0xAD)]
    public void MediaKey_MapsToTheWindowsCode(MediaKeyKind key, int expected)
    {
        Assert.Equal(expected, (int)KeyCodes.For(key));
    }

    [Fact]
    public void UndefinedValues_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyCodes.For((VirtualKey)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyCodes.For((MediaKeyKind)999));
    }

    [Theory]
    [InlineData(VirtualKey.Up, true)]
    [InlineData(VirtualKey.Delete, true)]
    [InlineData(VirtualKey.PageDown, true)]
    [InlineData(VirtualKey.A, false)]
    [InlineData(VirtualKey.Enter, false)]
    [InlineData(VirtualKey.F5, false)]
    [InlineData(VirtualKey.NumPadDivide, true)]
    [InlineData(VirtualKey.NumLock, true)]
    [InlineData(VirtualKey.NumPad8, false)]
    [InlineData(VirtualKey.NumPadAdd, false)]
    [InlineData(VirtualKey.NumPadSubtract, false)]
    public void ExtendedKeys_AreFlagged(VirtualKey key, bool extended)
    {
        Assert.Equal(extended, KeyCodes.IsExtended(KeyCodes.For(key)));
    }
}

public sealed class KeySequenceTests
{
    private static KeyStroke Down(VIRTUAL_KEY key, bool extended = false) => new(key, IsKeyUp: false, extended);

    private static KeyStroke Up(VIRTUAL_KEY key, bool extended = false) => new(key, IsKeyUp: true, extended);

    [Fact]
    public void Hotkey_PressesModifiersThenTheKeyAndReleasesInReverse()
    {
        var strokes = KeySequence.For(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, VirtualKey.S));

        Assert.Equal(
            [
                Down(VIRTUAL_KEY.VK_LCONTROL),
                Down(VIRTUAL_KEY.VK_LSHIFT),
                Down(VIRTUAL_KEY.VK_S),
                Up(VIRTUAL_KEY.VK_S),
                Up(VIRTUAL_KEY.VK_LSHIFT),
                Up(VIRTUAL_KEY.VK_LCONTROL),
            ],
            strokes);
    }

    [Fact]
    public void Hotkey_UsesTheCanonicalModifierOrder()
    {
        var strokes = KeySequence.For(
            new Hotkey(KeyModifiers.Win | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Ctrl, VirtualKey.F5));

        Assert.Equal(
            [VIRTUAL_KEY.VK_LCONTROL, VIRTUAL_KEY.VK_LSHIFT, VIRTUAL_KEY.VK_LMENU, VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_F5],
            strokes.Where(stroke => !stroke.IsKeyUp).Select(stroke => stroke.Key));
        Assert.True(strokes.Single(stroke => stroke.Key == VIRTUAL_KEY.VK_LWIN && !stroke.IsKeyUp).IsExtended);
    }

    [Fact]
    public void Hotkey_WithoutModifiers_PressesOnlyTheKey()
    {
        Assert.Equal(
            [Down(VIRTUAL_KEY.VK_UP, extended: true), Up(VIRTUAL_KEY.VK_UP, extended: true)],
            KeySequence.For(new Hotkey(KeyModifiers.None, VirtualKey.Up)));
    }

    [Fact]
    public void MediaKey_IsOneExtendedPress()
    {
        Assert.Equal(
            [Down(VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE, extended: true), Up(VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE, extended: true)],
            KeySequence.For(new MediaKey(MediaKeyKind.PlayPause)));
    }
}
