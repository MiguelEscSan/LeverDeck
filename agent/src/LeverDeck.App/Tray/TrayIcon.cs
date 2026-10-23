using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LeverDeck.Application.Platform.Ports;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.App.Tray;

/// <summary>
/// The notification-area icon (Phase 7): a right-click menu with "Open editor", "Start with Windows" and "Exit". It runs
/// its own message loop on a background thread, so the web host is not blocked. A failure to create the icon is logged
/// and the agent keeps serving.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class TrayIcon(
    LeverDeckOptions options,
    AdminTokenStore adminTokens,
    IStartupRegistration startup,
    IHostApplicationLifetime lifetime,
    ILogger<TrayIcon> logger) : IHostedService, IDisposable
{
    private const string ClassName = "LeverDeckTray";
    private const string Tooltip = "LeverDeck";
    private const string TaskbarCreatedMessage = "TaskbarCreated";
    private const uint IconId = 1;
    private const nuint OpenEditorId = 1;
    private const nuint StartupId = 2;
    private const nuint ExitId = 3;

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(5);

    // The window procedure is static (a native callback), so the running icon is reached through this field.
    private static TrayIcon? _current;

    private readonly ManualResetEventSlim _ready = new(initialState: false);
    private Thread? _thread;
    private nint _window;
    private uint _taskbarCreated;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _current = this;
        _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "LeverDeck tray" };
        _thread.Start();
        if (!_ready.Wait(ReadyTimeout, cancellationToken))
        {
            LogNotReady();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_window != 0)
        {
            // WM_CLOSE destroys the window; WM_DESTROY removes the icon and ends the message loop.
            NativeTray.PostMessage(_window, NativeTray.WmClose, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _current = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _ready.Dispose();

    /// <summary>Routes a window message of the tray window. Called by the native window procedure.</summary>
    internal static nint HandleMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        var tray = _current;
        if (tray is null)
        {
            return NativeTray.DefWindowProc(hwnd, message, wParam, lParam);
        }

        try
        {
            if (message == NativeTray.WmTrayIcon)
            {
                var mouseMessage = (uint)(lParam & 0xFFFF);
                if (mouseMessage is NativeTray.WmRButtonUp or NativeTray.WmLButtonUp)
                {
                    tray.ShowMenu(hwnd);
                }

                return 0;
            }

            if (message == NativeTray.WmDestroy)
            {
                tray.RemoveIcon();
                NativeTray.PostQuitMessage(0);
                return 0;
            }

            if (tray._taskbarCreated != 0 && message == tray._taskbarCreated)
            {
                // Explorer restarted and dropped the icon: add it again.
                tray.AddIcon();
                return 0;
            }
        }
        catch (Exception exception)
        {
            tray.LogHandlerFailed(exception.Message);
        }

        return NativeTray.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void RunMessageLoop()
    {
        var className = Marshal.StringToHGlobalUni(ClassName);
        try
        {
            var instance = NativeTray.GetModuleHandle(0);
            var windowClass = new NativeTray.WndClassEx
            {
                cbSize = (uint)Unsafe.SizeOf<NativeTray.WndClassEx>(),
                lpfnWndProc = NativeTray.WindowProcedureAddress(),
                hInstance = instance,
                lpszClassName = className,
            };

            var atom = NativeTray.RegisterClassEx(ref windowClass);
            if (atom == 0)
            {
                LogNativeFailed("RegisterClassEx", Marshal.GetLastPInvokeError());
                return;
            }

            _window = NativeTray.CreateWindowEx(
                0, atom, 0, 0, 0, 0, 0, 0, NativeTray.MessageOnlyParent, 0, instance, 0);
            if (_window == 0)
            {
                LogNativeFailed("CreateWindowEx", Marshal.GetLastPInvokeError());
                return;
            }

            _taskbarCreated = NativeTray.RegisterWindowMessage(TaskbarCreatedMessage);
            AddIcon();
            _ready.Set();

            while (NativeTray.GetMessage(out var message, 0, 0, 0) > 0)
            {
                NativeTray.TranslateMessage(ref message);
                NativeTray.DispatchMessage(ref message);
            }
        }
        finally
        {
            _ready.Set();
            Marshal.FreeHGlobal(className);
        }
    }

    private void AddIcon() => ChangeIcon(NativeTray.NimAdd, NativeTray.NifMessage | NativeTray.NifIcon | NativeTray.NifTip);

    private void RemoveIcon()
    {
        var data = new NativeTray.NotifyIconData
        {
            cbSize = (uint)Unsafe.SizeOf<NativeTray.NotifyIconData>(),
            hWnd = _window,
            uID = IconId,
        };
        NativeTray.Shell_NotifyIcon(NativeTray.NimDelete, ref data);
    }

    private void ChangeIcon(uint action, uint flags)
    {
        var data = new NativeTray.NotifyIconData
        {
            cbSize = (uint)Unsafe.SizeOf<NativeTray.NotifyIconData>(),
            hWnd = _window,
            uID = IconId,
            uFlags = flags,
            uCallbackMessage = NativeTray.WmTrayIcon,
            hIcon = AppIcon(),
        };

        Tooltip.AsSpan().CopyTo(new Span<char>(data.szTip, Tooltip.Length));

        if (!NativeTray.Shell_NotifyIcon(action, ref data))
        {
            LogNativeFailed("Shell_NotifyIcon", Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// The exe's own icon (<c>ApplicationIcon</c> in the project) at the small-icon size the notification area shows, so
    /// Windows picks the .ico's own small frame instead of scaling the large one down; the system's generic icon when
    /// the exe has none (a build without the icon). Shared, so repeated calls return the same handle and leak nothing.
    /// </summary>
    private static nint AppIcon()
    {
        var own = NativeTray.LoadImage(
            NativeTray.GetModuleHandle(0),
            NativeTray.OwnIconId,
            NativeTray.ImageIcon,
            NativeTray.GetSystemMetrics(NativeTray.SmCxSmIcon),
            NativeTray.GetSystemMetrics(NativeTray.SmCySmIcon),
            NativeTray.LrShared);
        return own != 0 ? own : NativeTray.LoadIcon(0, NativeTray.ApplicationIcon);
    }

    private void ShowMenu(nint hwnd)
    {
        NativeTray.GetCursorPos(out var point);
        NativeTray.SetForegroundWindow(hwnd);

        var menu = NativeTray.CreatePopupMenu();
        try
        {
            NativeTray.AppendMenu(menu, NativeTray.MfString, OpenEditorId, "Open editor");
            var startupFlags = NativeTray.MfString | (IsStartupEnabled() ? NativeTray.MfChecked : 0u);
            NativeTray.AppendMenu(menu, startupFlags, StartupId, "Start with Windows");
            NativeTray.AppendMenu(menu, NativeTray.MfSeparator, 0, null);
            NativeTray.AppendMenu(menu, NativeTray.MfString, ExitId, "Exit LeverDeck");

            var selected = NativeTray.TrackPopupMenu(
                menu, NativeTray.TpmRightButton | NativeTray.TpmReturnCmd, point.x, point.y, 0, hwnd, 0);
            if (selected != 0)
            {
                Handle((nuint)selected);
            }
        }
        finally
        {
            NativeTray.DestroyMenu(menu);
        }
    }

    private void Handle(nuint command)
    {
        switch (command)
        {
            case OpenEditorId:
                OpenEditor();
                break;
            case StartupId:
                ToggleStartup();
                break;
            case ExitId:
                lifetime.StopApplication();
                break;
        }
    }

    private void OpenEditor()
    {
        var scheme = options.HttpsEnabled ? "https" : "http";
        var url = $"{scheme}://localhost:{LeverDeckOptions.Port}/?admin={Uri.EscapeDataString(adminTokens.GetOrCreate())}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LogActionFailed(exception.Message);
        }
    }

    private bool IsStartupEnabled()
    {
        try
        {
            return startup.IsEnabled();
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogActionFailed(exception.Message);
            return false;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            startup.SetEnabled(!startup.IsEnabled());
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogActionFailed(exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tray icon did not start within the time limit.")]
    private partial void LogNotReady();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tray icon could not be created: {Step} failed with Win32 error {Error}.")]
    private partial void LogNativeFailed(string step, int error);

    [LoggerMessage(Level = LogLevel.Error, Message = "A tray message could not be handled: {Message}")]
    private partial void LogHandlerFailed(string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tray action could not be completed: {Message}")]
    private partial void LogActionFailed(string message);
}
