using System.Diagnostics;
using System.Text.Json;
using LeverDeck.Application.Settings;
using LeverDeck.Application.Settings.Ports;

namespace LeverDeck.Infrastructure.Apps;

/// <summary>
/// The Microsoft Store apps in the Start menu, read with <c>Get-StartApps</c> on each call (apps come and go). Only the
/// shell's own report is used: the AppUserModelIds are validated later by <see cref="LeverDeck.Domain.Settings.RegisteredApp"/>.
/// </summary>
public sealed class StartMenuStoreAppCatalog : IStoreAppCatalog
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task<IReadOnlyList<StoreApp>> ListInstalledAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<StoreApp>>(() => Parse(RunGetStartApps()), cancellationToken);

    /// <summary>Reads the JSON that <c>ConvertTo-Json</c> writes: an array, or one object when there is a single app.</summary>
    internal static IReadOnlyList<StoreApp> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var entries = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root];
            return
            [
                .. entries
                    .Where(entry => entry.TryGetProperty("AppID", out _) && entry.TryGetProperty("Name", out _))
                    .Select(entry => new StoreApp(entry.GetProperty("Name").GetString() ?? string.Empty, entry.GetProperty("AppID").GetString() ?? string.Empty)),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string RunGetStartApps()
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("Get-StartApps | ConvertTo-Json -Compress");

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            return process.WaitForExit(Timeout) ? output : string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
