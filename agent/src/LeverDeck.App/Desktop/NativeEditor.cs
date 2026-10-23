using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LeverDeck.App.Tray;

namespace LeverDeck.App.Desktop;

/// <summary>
/// The Win32 calls behind the editor window (a top-level window that hosts WebView2) and the single-instance signal.
/// <c>LibraryImport</c> and blittable structs, like <see cref="NativeTray"/>, so it stays Native AOT compatible.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeEditor
{
    public const uint WmSize = 0x0005;
    public const uint WmSetFocus = 0x0007;
    public const uint WmClose = 0x0010;
    public const uint WmGetMinMaxInfo = 0x0024;
    public const uint WmDpiChanged = 0x02E0;

    public const nint SizeMinimized = 1;

    public const uint WsOverlappedWindow = 0x00CF0000;
    public const int CwUseDefault = unchecked((int)0x80000000);

    public const int SwHide = 0;
    public const int SwShowNormal = 1;
    public const int SwShowMaximized = 3;
    public const int SwShow = 5;
    public const int SwRestore = 9;

    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;

    public const uint MbOk = 0x0;
    public const uint MbYesNo = 0x4;
    public const uint MbIconWarning = 0x30;
    public const uint MbSetForeground = 0x10000;
    public const int IdYes = 6;

    /// <summary>LR_DEFAULTSIZE: the icon at the system's large-icon size.</summary>
    public const uint LrDefaultSize = 0x40;

    /// <summary>COLOR_WINDOW + 1, the system window background as a class brush.</summary>
    public static readonly nint WindowBackgroundBrush = 6;

    /// <summary>IDC_ARROW.</summary>
    public static readonly nint ArrowCursor = 32512;

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.</summary>
    public static readonly nint PerMonitorAwareV2 = -4;

    /// <summary>The DPI Windows designs for; window sizes below are in these units and scaled to the monitor's DPI.</summary>
    public const int DefaultDpi = 96;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    /// <summary>WINDOWPLACEMENT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Placement
    {
        public uint length;
        public uint flags;
        public uint showCmd;
        public NativeTray.Point ptMinPosition;
        public NativeTray.Point ptMaxPosition;
        public Rect rcNormalPosition;
    }

    /// <summary>MINMAXINFO.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo
    {
        public NativeTray.Point ptReserved;
        public NativeTray.Point ptMaxSize;
        public NativeTray.Point ptMaxPosition;
        public NativeTray.Point ptMinTrackSize;
        public NativeTray.Point ptMaxTrackSize;
    }

    public static uint PlacementSize => (uint)Unsafe.SizeOf<Placement>();

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(
        uint exStyle,
        nint classAtom,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint param);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "IsZoomed")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsZoomed(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowPlacement", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowPlacement(nint hwnd, ref Placement placement);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPlacement", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPlacement(nint hwnd, ref Placement placement);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    public static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    public static partial nint LoadCursor(nint instance, nint cursorName);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(nint hwnd, string text, string caption, uint type);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint childAfter, string className, string? windowName);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "AllowSetForegroundWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllowSetForegroundWindow(uint processId);

    /// <summary>Scales a length in 96-DPI units to <paramref name="dpi"/>.</summary>
    public static int Scale(int length, uint dpi) => (int)(length * (long)dpi / DefaultDpi);

    /// <summary>The editor's window procedure, called by Windows on the UI (tray) thread.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam) =>
        EditorWindow.HandleMessage(hwnd, message, wParam, lParam);

    /// <summary>The address of <see cref="WindowProcedure"/> for <c>WNDCLASSEXW.lpfnWndProc</c>.</summary>
    public static nint WindowProcedureAddress() =>
        (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WindowProcedure;
}
