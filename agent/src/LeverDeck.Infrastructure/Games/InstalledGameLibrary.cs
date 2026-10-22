using LeverDeck.Application.Games;
using LeverDeck.Application.Games.Ports;
using Microsoft.Win32;

namespace LeverDeck.Infrastructure.Games;

/// <summary>
/// The installed Steam and Epic games, read from the launchers' own files on each call (they change whenever a game is
/// installed or removed). Steam's folder comes from its registry key, else the default under Program Files (x86); Epic's
/// manifests live under ProgramData.
/// </summary>
public sealed class InstalledGameLibrary : IGameLibrary
{
    private readonly SteamLibrary _steam;
    private readonly EpicLibrary _epic;

    public InstalledGameLibrary()
        : this(new SteamLibrary(FindSteamRoot), new EpicLibrary(EpicManifestsFolder))
    {
    }

    internal InstalledGameLibrary(SteamLibrary steam, EpicLibrary epic)
    {
        _steam = steam;
        _epic = epic;
    }

    public Task<IReadOnlyList<InstalledGame>> ListInstalledAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<InstalledGame>>(() => [.. _steam.ListInstalled(), .. _epic.ListInstalled()], cancellationToken);

    private static string? FindSteamRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string path && !string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Fall back to the default folder.
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return string.IsNullOrEmpty(programFiles) ? null : Path.Combine(programFiles, "Steam");
    }

    private static string? EpicManifestsFolder()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return string.IsNullOrEmpty(programData)
            ? null
            : Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
    }
}
