using LeverDeck.Infrastructure.Platform;

namespace LeverDeck.App.Desktop;

/// <summary>How this process was started.</summary>
public enum LaunchKind
{
    /// <summary>The owner opened LeverDeck (Start menu, shortcut, the installer's first run).</summary>
    Manual,

    /// <summary>Windows started it at sign-in through the Run key (<see cref="RunKeyStartupRegistration.BackgroundSwitch"/>).</summary>
    SignIn,
}

/// <summary>What this process does, given whether another instance already runs and how it was started.</summary>
public enum InstanceAction
{
    RunAndShowWindow,
    RunInBackground,
    ShowRunningInstanceAndExit,
    ExitQuietly,
}

/// <summary>
/// The single-instance rule of the desktop app (ROADMAP Phase 8b), kept apart from the Windows calls in
/// <see cref="SingleInstance"/> so it can be tested anywhere: one agent per Windows session; a manual launch shows the
/// window (its own, or the running instance's), the sign-in start stays in the tray.
/// </summary>
internal static class Launch
{
    public static LaunchKind FromArguments(IReadOnlyList<string> args) =>
        args.Any(IsBackgroundSwitch) ? LaunchKind.SignIn : LaunchKind.Manual;

    /// <summary>
    /// The arguments for the web host: everything but the launch switch, which its command-line configuration would
    /// read as a key taking the next argument as its value.
    /// </summary>
    public static string[] HostArguments(IReadOnlyList<string> args) => [.. args.Where(argument => !IsBackgroundSwitch(argument))];

    public static InstanceAction Decide(bool isFirstInstance, LaunchKind launch) => (isFirstInstance, launch) switch
    {
        (true, LaunchKind.Manual) => InstanceAction.RunAndShowWindow,
        (true, _) => InstanceAction.RunInBackground,
        (false, LaunchKind.Manual) => InstanceAction.ShowRunningInstanceAndExit,
        (false, _) => InstanceAction.ExitQuietly,
    };

    public static bool RunsTheAgent(this InstanceAction action) =>
        action is InstanceAction.RunAndShowWindow or InstanceAction.RunInBackground;

    private static bool IsBackgroundSwitch(string argument) =>
        string.Equals(argument, RunKeyStartupRegistration.BackgroundSwitch, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Whether the editor window opens once the agent is serving; registered for the desktop app.</summary>
internal sealed record DesktopLaunch(bool ShowWindowOnStart);
