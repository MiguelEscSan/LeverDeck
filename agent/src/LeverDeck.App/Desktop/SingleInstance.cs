using System.Diagnostics;
using System.Runtime.Versioning;
using LeverDeck.App.Tray;

namespace LeverDeck.App.Desktop;

/// <summary>
/// One agent per Windows session: a named mutex in the session's <c>Local\</c> namespace marks the running instance,
/// and a second launch reaches it through its tray window (a message-only window found by class name) with
/// <see cref="NativeTray.WmShowEditor"/>. The decision of what to do lives in <see cref="Launch"/>. Acquire and
/// dispose it on the same thread (the main thread), which owns the mutex.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\LeverDeck.Agent";

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex, bool isFirst)
    {
        _mutex = mutex;
        IsFirst = isFirst;
    }

    /// <summary>Whether this process holds the mutex, so it is the one agent of the session.</summary>
    public bool IsFirst { get; }

    public static SingleInstance Acquire()
    {
        // A crashed instance's mutex is gone with its process, so createdNew is true again for the next start.
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return new SingleInstance(mutex, createdNew);
    }

    /// <summary>
    /// Asks the running instance to show its window. It may still be starting, so its tray window is looked for until
    /// <paramref name="timeout"/>. Lets it take the foreground, which only the process the owner just launched may grant.
    /// </summary>
    public static bool ShowRunningInstance(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var tray = NativeEditor.FindWindowEx(NativeTray.MessageOnlyParent, 0, TrayIcon.ClassName, null);
            if (tray != 0)
            {
                NativeEditor.GetWindowThreadProcessId(tray, out var processId);
                NativeEditor.AllowSetForegroundWindow(processId);
                return NativeTray.PostMessage(tray, NativeTray.WmShowEditor, 0, 0);
            }

            if (clock.Elapsed >= timeout)
            {
                return false;
            }

            Thread.Sleep(RetryInterval);
        }
    }

    public void Dispose()
    {
        if (IsFirst)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
