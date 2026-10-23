using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LeverDeck.App.Tray;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Desktop;
using LeverDeck.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace LeverDeck.App.Desktop;

/// <summary>
/// LeverDeck's own window: the existing Angular editor (<see cref="EditorUrl"/>) in WebView2, so the owner needs no
/// browser and never sees the admin token. It lives on the tray's UI thread (STA, one message loop): every member
/// must be called there. Created on first <see cref="Show"/>; closing it only hides it (the agent keeps serving the
/// phones from the tray) and remembers its size and position; <see cref="Destroy"/> runs when the agent exits.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class EditorWindow(
    LeverDeckOptions options,
    AdminTokenStore adminTokens,
    WindowPlacementStore placements,
    ILogger<EditorWindow> logger)
{
    private const string ClassName = "LeverDeckEditor";
    private const string Title = "LeverDeck";
    private const int DefaultWidth = 1200;
    private const int DefaultHeight = 800;
    private const string RuntimeDownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    // The window procedure is static (a native callback), so the window is reached through this field.
    private static EditorWindow? _current;
    private static ushort _classAtom;

    private nint _window;
    private bool _firstShow = true;
    private bool _maximizeOnFirstShow;
    private bool _creatingWebView;
    private ICoreWebView2Controller? _controller;

    /// <summary>Shows the window (creating it and its web view the first time) and brings it to the front.</summary>
    public void Show()
    {
        if (_window == 0 && !Create())
        {
            return;
        }

        if (_firstShow)
        {
            _firstShow = false;
            NativeEditor.ShowWindow(_window, _maximizeOnFirstShow ? NativeEditor.SwShowMaximized : NativeEditor.SwShowNormal);
        }
        else
        {
            NativeEditor.ShowWindow(_window, NativeEditor.IsIconic(_window) ? NativeEditor.SwRestore : NativeEditor.SwShow);
        }

        NativeTray.SetForegroundWindow(_window);

        if (_controller is not null)
        {
            Try(() =>
            {
                _controller.SetIsVisible(true);
                _controller.MoveFocus(0);
            });
        }
        else if (!_creatingWebView)
        {
            StartWebView();
        }
    }

    /// <summary>Closes the web view and destroys the window, before the agent exits.</summary>
    public void Destroy()
    {
        if (_window == 0)
        {
            return;
        }

        if (NativeEditor.IsWindowVisible(_window))
        {
            SavePlacement();
        }

        // The spike showed the browser processes must be told to close before the process ends.
        var controller = _controller;
        _controller = null;
        if (controller is not null)
        {
            Try(controller.Close);
        }

        NativeTray.DestroyWindow(_window);
        _window = 0;
        _current = null;
    }

    /// <summary>Routes a message of the editor window. Called by the native window procedure.</summary>
    internal static nint HandleMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        var editor = _current;
        if (editor is null || editor._window != hwnd)
        {
            return NativeTray.DefWindowProc(hwnd, message, wParam, lParam);
        }

        try
        {
            switch (message)
            {
                case NativeEditor.WmClose:
                    editor.Hide();
                    return 0;
                case NativeEditor.WmSize when wParam != NativeEditor.SizeMinimized:
                    editor.ApplyBounds();
                    break;
                case NativeEditor.WmDpiChanged:
                    editor.MoveToSuggestedRect(lParam);
                    return 0;
                case NativeEditor.WmGetMinMaxInfo:
                    SetMinimumSize(hwnd, lParam);
                    return 0;
                case NativeEditor.WmSetFocus when editor._controller is not null:
                    editor._controller.MoveFocus(0);
                    break;
            }
        }
        catch (Exception exception)
        {
            editor.LogHandlerFailed(exception.Message);
        }

        return NativeTray.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private bool Create()
    {
        var instance = NativeTray.GetModuleHandle(0);
        if (_classAtom == 0)
        {
            var className = Marshal.StringToHGlobalUni(ClassName);
            try
            {
                var windowClass = new NativeTray.WndClassEx
                {
                    cbSize = (uint)Unsafe.SizeOf<NativeTray.WndClassEx>(),
                    lpfnWndProc = NativeEditor.WindowProcedureAddress(),
                    hInstance = instance,
                    hIcon = NativeTray.LoadImage(instance, NativeTray.OwnIconId, NativeTray.ImageIcon, 0, 0, NativeTray.LrShared | NativeEditor.LrDefaultSize),
                    hIconSm = NativeTray.LoadImage(
                        instance,
                        NativeTray.OwnIconId,
                        NativeTray.ImageIcon,
                        NativeTray.GetSystemMetrics(NativeTray.SmCxSmIcon),
                        NativeTray.GetSystemMetrics(NativeTray.SmCySmIcon),
                        NativeTray.LrShared),
                    hCursor = NativeEditor.LoadCursor(0, NativeEditor.ArrowCursor),
                    hbrBackground = NativeEditor.WindowBackgroundBrush,
                    lpszClassName = className,
                };

                // The class keeps the pointer only during registration; Windows copies the name.
                _classAtom = NativeTray.RegisterClassEx(ref windowClass);
            }
            finally
            {
                Marshal.FreeHGlobal(className);
            }

            if (_classAtom == 0)
            {
                LogNativeFailed("RegisterClassEx", Marshal.GetLastPInvokeError());
                return false;
            }
        }

        var dpi = NativeEditor.GetDpiForSystem();
        _current = this;
        _window = NativeEditor.CreateWindowEx(
            0,
            _classAtom,
            Title,
            NativeEditor.WsOverlappedWindow,
            NativeEditor.CwUseDefault,
            NativeEditor.CwUseDefault,
            NativeEditor.Scale(DefaultWidth, dpi),
            NativeEditor.Scale(DefaultHeight, dpi),
            0,
            0,
            instance,
            0);
        if (_window == 0)
        {
            _current = null;
            LogNativeFailed("CreateWindowEx", Marshal.GetLastPInvokeError());
            return false;
        }

        RestorePlacement();
        return true;
    }

    private void StartWebView()
    {
        _creatingWebView = true;
        int result;
        try
        {
            // Always our own folder: the default, next to the exe, is replaced on every Velopack update.
            Directory.CreateDirectory(options.WebView2DataDirectory);
            result = WebView2Loader.CreateCoreWebView2EnvironmentWithOptions(
                null, options.WebView2DataDirectory, 0, new EnvironmentCompletedHandler(OnEnvironmentCreated));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException)
        {
            LogWebViewFailed(exception.Message);
            result = WebView2Loader.RuntimeNotFound;
        }

        // The completed callback may already have run, re-entrantly, inside the call above; if it failed, it reported.
        if (result < 0 && _creatingWebView)
        {
            Fail(result);
        }
    }

    private void OnEnvironmentCreated(int errorCode, ICoreWebView2Environment? environment)
    {
        if (!_creatingWebView)
        {
            return;
        }

        if (errorCode < 0 || environment is null || _window == 0)
        {
            Fail(errorCode < 0 ? errorCode : WebView2Loader.RuntimeNotFound);
            return;
        }

        try
        {
            environment.CreateCoreWebView2Controller(_window, new ControllerCompletedHandler(OnControllerCreated));
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            Fail(exception.HResult);
        }
    }

    private void OnControllerCreated(int errorCode, ICoreWebView2Controller? controller)
    {
        _creatingWebView = false;
        if (errorCode < 0 || controller is null)
        {
            Fail(errorCode < 0 ? errorCode : WebView2Loader.RuntimeNotFound);
            return;
        }

        if (_window == 0)
        {
            // The agent exited while the web view was being created.
            Try(controller.Close);
            return;
        }

        _controller = controller;
        ApplyBounds();
        var visible = NativeEditor.IsWindowVisible(_window);
        Try(() =>
        {
            controller.SetIsVisible(visible);
            controller.GetCoreWebView2().Navigate(EditorUrl.For(options.HttpsEnabled, adminTokens.GetOrCreate()));
            if (visible)
            {
                controller.MoveFocus(0);
            }
        });
    }

    /// <summary>
    /// The web view could not start. A missing WebView2 Runtime gets a way to install it; anything else is logged and
    /// reported. The window is hidden again, the tray and the phones keep working, and the next "Open LeverDeck" retries.
    /// </summary>
    private void Fail(int result)
    {
        _creatingWebView = false;
        LogWebViewHResult(result);
        NativeEditor.ShowWindow(_window, NativeEditor.SwHide);

        if (result == WebView2Loader.RuntimeNotFound)
        {
            var answer = NativeEditor.MessageBox(
                0,
                "LeverDeck shows its window with the Microsoft Edge WebView2 Runtime, which is not installed on this PC.\n\n"
                + "It comes with Windows 11 and with Windows 10 updates, or you can install it from Microsoft. Phones keep "
                + "working in the meantime.\n\nOpen the WebView2 download page now?",
                Title,
                NativeEditor.MbYesNo | NativeEditor.MbIconWarning | NativeEditor.MbSetForeground);
            if (answer == NativeEditor.IdYes)
            {
                OpenInBrowser(RuntimeDownloadUrl);
            }

            return;
        }

        // No owner window (the editor is hidden again): MB_SETFOREGROUND keeps the message from opening behind others.
        NativeEditor.MessageBox(
            0,
            $"The LeverDeck window could not start (error 0x{result:X8}). Phones keep working.\n\n"
            + $"Details are in the log: {options.LogDirectory}",
            Title,
            NativeEditor.MbOk | NativeEditor.MbIconWarning | NativeEditor.MbSetForeground);
    }

    private void Hide()
    {
        SavePlacement();
        NativeEditor.ShowWindow(_window, NativeEditor.SwHide);
        if (_controller is not null)
        {
            // A hidden web view uses less CPU and memory; it stays loaded, so reopening is instant.
            Try(() => _controller.SetIsVisible(false));
        }
    }

    private void ApplyBounds()
    {
        if (_controller is not null && NativeEditor.GetClientRect(_window, out var client))
        {
            _controller.SetBounds(client);
        }
    }

    /// <summary>WM_DPICHANGED: take the size Windows suggests for the new monitor; WM_SIZE then resizes the web view.</summary>
    private unsafe void MoveToSuggestedRect(nint suggested)
    {
        var rect = *(NativeEditor.Rect*)suggested;
        NativeEditor.SetWindowPos(
            _window,
            0,
            rect.left,
            rect.top,
            rect.right - rect.left,
            rect.bottom - rect.top,
            NativeEditor.SwpNoZOrder | NativeEditor.SwpNoActivate);
        ApplyBounds();
    }

    private static unsafe void SetMinimumSize(nint hwnd, nint minMaxInfo)
    {
        var dpi = NativeEditor.GetDpiForWindow(hwnd);
        var info = (NativeEditor.MinMaxInfo*)minMaxInfo;
        info->ptMinTrackSize.x = NativeEditor.Scale(WindowPlacement.MinimumWidth, dpi);
        info->ptMinTrackSize.y = NativeEditor.Scale(WindowPlacement.MinimumHeight, dpi);
    }

    private void RestorePlacement()
    {
        if (placements.Load() is not { } saved)
        {
            return;
        }

        // SetWindowPlacement moves a window that would be off every screen (a monitor unplugged) back onto one.
        var placement = new NativeEditor.Placement
        {
            length = NativeEditor.PlacementSize,
            showCmd = NativeEditor.SwHide,
            rcNormalPosition = new NativeEditor.Rect
            {
                left = saved.X,
                top = saved.Y,
                right = saved.X + saved.Width,
                bottom = saved.Y + saved.Height,
            },
        };
        if (!NativeEditor.SetWindowPlacement(_window, ref placement))
        {
            LogNativeFailed("SetWindowPlacement", Marshal.GetLastPInvokeError());
            return;
        }

        // Still hidden; the first Show maximizes it.
        _maximizeOnFirstShow = saved.Maximized;
    }

    private void SavePlacement()
    {
        var placement = new NativeEditor.Placement { length = NativeEditor.PlacementSize };
        if (!NativeEditor.GetWindowPlacement(_window, ref placement))
        {
            return;
        }

        var normal = placement.rcNormalPosition;
        try
        {
            placements.Save(new WindowPlacement(
                normal.left,
                normal.top,
                normal.right - normal.left,
                normal.bottom - normal.top,
                NativeEditor.IsZoomed(_window)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogWebViewFailed(exception.Message);
        }
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LogWebViewFailed(exception.Message);
        }
    }

    private void Try(Action call)
    {
        try
        {
            call();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            LogWebViewFailed(exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The editor window could not be created: {Step} failed with Win32 error {Error}.")]
    private partial void LogNativeFailed(string step, int error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The editor's web view could not start: HRESULT 0x{Result:X8}.")]
    private partial void LogWebViewHResult(int result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The editor window could not complete an action: {Message}")]
    private partial void LogWebViewFailed(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "An editor window message could not be handled: {Message}")]
    private partial void LogHandlerFailed(string message);
}
