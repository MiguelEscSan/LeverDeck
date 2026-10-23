using System.Runtime.Versioning;
using LeverDeck.Application.Platform.Ports;
using LeverDeck.Infrastructure.Platform;

namespace LeverDeck.App.Desktop;

/// <summary>
/// What LeverDeck does in Velopack's install hooks. Velopack itself installs per user under
/// <c>%LocalAppData%\LeverDeck</c>, adds the Start menu shortcut and removes both on uninstall; the data directory
/// (<c>%APPDATA%\LeverDeck</c>: profiles, paired devices, logs) is the owner's and is kept.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Installer
{
    /// <summary>
    /// Turns on "Start with Windows" for a new install, like other tray apps, so the phones find the PC after every
    /// sign-in. Velopack runs this hook on the first install only, not on updates, so an owner who turned it off keeps
    /// it off. The exe runs from Velopack's <c>current</c> folder, which updates keep, so the Run value stays valid.
    /// </summary>
    public static void AfterInstall() => AfterInstall(new RunKeyStartupRegistration());

    /// <summary>Removes the sign-in start, so Windows does not try to run an uninstalled exe.</summary>
    public static void BeforeUninstall() => BeforeUninstall(new RunKeyStartupRegistration());

    internal static void AfterInstall(IStartupRegistration startup) => SetStartup(startup, enabled: true);

    internal static void BeforeUninstall(IStartupRegistration startup) => SetStartup(startup, enabled: false);

    private static void SetStartup(IStartupRegistration startup, bool enabled)
    {
        try
        {
            startup.SetEnabled(enabled);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Nothing to report to: installing or uninstalling goes on. Without the value LeverDeck simply does not
            // start at sign-in (the tray item turns it on); a stale one only fails silently at sign-in.
        }
    }
}
