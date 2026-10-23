using System.Runtime.Versioning;
using LeverDeck.Application.Platform.Ports;
using Microsoft.Win32;

namespace LeverDeck.Infrastructure.Platform;

/// <summary>
/// Starts the agent with the owner's session through the per-user <c>Run</c> key, so no administrator rights are
/// needed. Only the published <c>leverdeck.exe</c> can be registered: a <c>dotnet run</c> build would register the
/// dotnet host instead.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "LeverDeck";

    private const string ExecutableName = "leverdeck.exe";

    private readonly string _executablePath;

    public RunKeyStartupRegistration()
        : this(Environment.ProcessPath ?? string.Empty)
    {
    }

    internal RunKeyStartupRegistration(string executablePath)
    {
        _executablePath = executablePath;
    }

    /// <summary>The command line written to the Run key for a published executable.</summary>
    public static string CommandFor(string executablePath)
    {
        if (!string.Equals(Path.GetFileName(executablePath), ExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Start with Windows needs the published {ExecutableName}; this process is '{executablePath}'.");
        }

        return $"\"{executablePath}\"";
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, CommandFor(_executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
