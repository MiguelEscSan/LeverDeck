using LeverDeck.Application.Games;
using LeverDeck.Domain.Actions;

namespace LeverDeck.Infrastructure.Games;

/// <summary>
/// The games Steam has installed: every library listed in <c>steamapps/libraryfolders.vdf</c> under the Steam folder,
/// and in each one the fully installed <c>appmanifest_*.acf</c> apps. Steam's own redistributables are left out.
/// Unreadable files are skipped; a missing Steam yields nothing.
/// </summary>
internal sealed class SteamLibrary(Func<string?> steamRoot)
{
    /// <summary>Steamworks Common Redistributables: installed with most games, never something to launch.</summary>
    private const string RedistributablesAppId = "228980";

    /// <summary><c>StateFlags</c> bit set when an app is fully installed.</summary>
    private const int FullyInstalled = 4;

    public IReadOnlyList<InstalledGame> ListInstalled()
    {
        var root = steamRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var games = new List<InstalledGame>();
        foreach (var library in LibraryFolders(root))
        {
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            foreach (var manifest in SafeFiles(steamApps, "appmanifest_*.acf"))
            {
                if (ReadManifest(manifest) is { } game)
                {
                    games.Add(game);
                }
            }
        }

        return games;
    }

    private static IEnumerable<string> LibraryFolders(string root)
    {
        var folders = new List<string> { root };
        var vdf = ReadVdf(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
        var list = vdf?.Section("libraryfolders");
        if (list is not null)
        {
            folders.AddRange(list.Sections.Select(section => section.Value("path")).OfType<string>());
        }

        return folders
            .Select(folder => Path.GetFullPath(folder.Replace('/', Path.DirectorySeparatorChar)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static InstalledGame? ReadManifest(string path)
    {
        var app = ReadVdf(path)?.Section("AppState");
        var appId = app?.Value("appid");
        if (app is null || appId is null || appId == RedistributablesAppId)
        {
            return null;
        }

        if (int.TryParse(app.Value("StateFlags"), out var flags) && (flags & FullyInstalled) == 0)
        {
            return null;
        }

        return new InstalledGame(GameStore.Steam, appId, app.Value("name") ?? appId);
    }

    private static VdfSection? ReadVdf(string path)
    {
        try
        {
            return File.Exists(path) ? VdfReader.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static IEnumerable<string> SafeFiles(string directory, string pattern)
    {
        try
        {
            return Directory.GetFiles(directory, pattern);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
