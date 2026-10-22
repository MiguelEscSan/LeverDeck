using System.ComponentModel;
using System.Diagnostics;

namespace LeverDeck.Infrastructure.Input;

/// <summary>What the agent hands to the Windows shell. Each call returns whether the shell accepted it.</summary>
internal interface IShellLauncher
{
    /// <summary>Opens a URI with its registered handler: the default browser, Steam or the Epic launcher.</summary>
    bool Open(Uri uri);

    /// <summary>Starts an executable with no arguments, in its own folder.</summary>
    bool Start(string executablePath);
}

/// <summary>
/// <see cref="IShellLauncher"/> over <c>ShellExecute</c> (<see cref="Process.Start(ProcessStartInfo)"/> with
/// <c>UseShellExecute</c>). Only URIs the domain validated and registered executable paths reach it, never arguments.
/// </summary>
internal sealed class WindowsShellLauncher : IShellLauncher
{
    public bool Open(Uri uri) => Run(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });

    public bool Start(string executablePath) => Run(new ProcessStartInfo(executablePath)
    {
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
    });

    private static bool Run(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }
}
