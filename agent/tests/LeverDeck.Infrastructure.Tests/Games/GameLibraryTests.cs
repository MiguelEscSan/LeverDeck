using LeverDeck.Application.Games;
using LeverDeck.Domain.Actions;
using LeverDeck.Infrastructure.Games;

namespace LeverDeck.Infrastructure.Tests.Games;

/// <summary>The Steam and Epic readers against fake launcher folders in a temp directory.</summary>
public sealed class GameLibraryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([_temp.Path, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Manifest(string steamApps, string appId, string name, int stateFlags = 4) =>
        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
            "AppState"
            {
                "appid"      "{{appId}}"
                "name"       "{{name}}"
                "StateFlags" "{{stateFlags}}"
            }
            """);

    [Fact]
    public void Steam_ListsFullyInstalledAppsOfEveryLibrary()
    {
        var root = Folder("Steam");
        var rootApps = Folder("Steam", "steamapps");
        var otherApps = Folder("Library2", "steamapps");
        var escaped = Path.Combine(_temp.Path, "Library2").Replace(@"\", @"\\", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(rootApps, "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0" { "path" "{{root.Replace(@"\", @"\\", StringComparison.Ordinal)}}" }
                "1" { "path" "{{escaped}}" }
                "2" { "path" "{{Path.Combine(_temp.Path, "Missing").Replace(@"\", @"\\", StringComparison.Ordinal)}}" }
            }
            """);
        Manifest(rootApps, "570", "Dota 2");
        Manifest(rootApps, "228980", "Steamworks Common Redistributables");
        Manifest(otherApps, "730", "Counter-Strike 2");
        Manifest(otherApps, "999", "Still downloading", stateFlags: 1026);
        File.WriteAllText(Path.Combine(otherApps, "appmanifest_1.acf"), "not { valid");

        var games = new SteamLibrary(() => root).ListInstalled();

        Assert.Equal(
            [new InstalledGame(GameStore.Steam, "570", "Dota 2"), new InstalledGame(GameStore.Steam, "730", "Counter-Strike 2")],
            games.OrderBy(game => game.GameId));
    }

    [Fact]
    public void Steam_NotInstalled_ListsNothing()
    {
        Assert.Empty(new SteamLibrary(() => Path.Combine(_temp.Path, "NoSteam")).ListInstalled());
        Assert.Empty(new SteamLibrary(() => null).ListInstalled());
    }

    [Fact]
    public void Epic_ListsCompleteApplicationInstalls()
    {
        var manifests = Folder("Manifests");
        File.WriteAllText(Path.Combine(manifests, "a.item"), """{ "AppName": "Fortnite", "DisplayName": "Fortnite", "bIsApplication": true }""");
        File.WriteAllText(Path.Combine(manifests, "b.item"), """{ "AppName": "Sugar", "DisplayName": "Rocket League", "bIsIncompleteInstall": false }""");
        File.WriteAllText(Path.Combine(manifests, "c.item"), """{ "AppName": "Half", "DisplayName": "Half done", "bIsIncompleteInstall": true }""");
        File.WriteAllText(Path.Combine(manifests, "d.item"), """{ "AppName": "Addon", "DisplayName": "An add-on", "bIsApplication": false }""");
        File.WriteAllText(Path.Combine(manifests, "e.item"), "{ broken json");
        File.WriteAllText(Path.Combine(manifests, "f.item"), """{ "AppName": "NoName" }""");

        var games = new EpicLibrary(() => manifests).ListInstalled();

        Assert.Equal(
            [
                new InstalledGame(GameStore.Epic, "Fortnite", "Fortnite"),
                new InstalledGame(GameStore.Epic, "NoName", "NoName"),
                new InstalledGame(GameStore.Epic, "Sugar", "Rocket League"),
            ],
            games.OrderBy(game => game.GameId));
    }

    [Fact]
    public async Task Library_CombinesBothStores()
    {
        var steamApps = Folder("Steam", "steamapps");
        Manifest(steamApps, "570", "Dota 2");
        var manifests = Folder("Manifests");
        File.WriteAllText(Path.Combine(manifests, "a.item"), """{ "AppName": "Fortnite", "DisplayName": "Fortnite" }""");
        var library = new InstalledGameLibrary(
            new SteamLibrary(() => Path.Combine(_temp.Path, "Steam")),
            new EpicLibrary(() => manifests));

        var games = await library.ListInstalledAsync(CancellationToken.None);

        Assert.Equal(["570", "Fortnite"], games.Select(game => game.GameId).Order());
    }
}
