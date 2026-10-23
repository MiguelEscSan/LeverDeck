using System.Runtime.Versioning;
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
    /// <summary>Removes the sign-in start, so Windows does not try to run an uninstalled exe.</summary>
    public static void BeforeUninstall()
    {
        try
        {
            new RunKeyStartupRegistration().SetEnabled(enabled: false);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Nothing to report to: uninstalling goes on, and a stale Run value only fails silently at sign-in.
        }
    }
}
