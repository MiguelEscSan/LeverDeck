using System.Diagnostics;
using System.Text;
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

    public async Task<IReadOnlyList<StoreApp>> ListInstalledAsync(CancellationToken cancellationToken) =>
        Parse(await RunAsync(GetStartAppsStartInfo(), Timeout, cancellationToken).ConfigureAwait(false));

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

    /// <summary>
    /// Runs a process and returns its standard output. Both streams are drained while the process runs, so a full
    /// pipe cannot deadlock it. When the timeout or the token fires first, the process tree is killed and the result is
    /// empty. A process that cannot be started also gives an empty result.
    /// </summary>
    internal static async Task<string> RunAsync(ProcessStartInfo start, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return string.Empty;
            }

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(timeout);
            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillTree(process);
                await Task.WhenAll(output, errors).ConfigureAwait(false);
                return string.Empty;
            }

            await Task.WhenAll(output, errors).ConfigureAwait(false);
            return output.Result;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Kills the process and its children; a process that already exited is left alone.</summary>
    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the timeout and the kill.
        }
    }

    /// <summary>
    /// A PowerShell call whose output is UTF-8: the console code page would turn accented names into replacement
    /// characters. The command runs as given, so callers pass only fixed scripts.
    /// </summary>
    internal static ProcessStartInfo CreatePowerShell(string command)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command}");
        return start;
    }

    private static ProcessStartInfo GetStartAppsStartInfo() =>
        CreatePowerShell("Get-StartApps | ConvertTo-Json -Compress");
}
