using System.Text.Json;
using LeverDeck.Application.Games;
using LeverDeck.Domain.Actions;

namespace LeverDeck.Infrastructure.Games;

/// <summary>
/// The games the Epic Games Launcher has installed: one JSON <c>*.item</c> manifest per install in its manifests
/// folder, keeping complete installs of applications (not add-ons). Unreadable files are skipped; a missing launcher
/// yields nothing.
/// </summary>
internal sealed class EpicLibrary(Func<string?> manifestsFolder)
{
    public IReadOnlyList<InstalledGame> ListInstalled()
    {
        var folder = manifestsFolder();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var games = new List<InstalledGame>();
        foreach (var manifest in SteamLibrary.SafeFiles(folder, "*.item"))
        {
            if (ReadManifest(manifest) is { } game)
            {
                games.Add(game);
            }
        }

        return games;
    }

    private static InstalledGame? ReadManifest(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("AppName", out var appName)
                || appName.ValueKind != JsonValueKind.String
                || IsTrue(root, "bIsIncompleteInstall")
                || IsFalse(root, "bIsApplication"))
            {
                return null;
            }

            var id = appName.GetString()!;
            var name = root.TryGetProperty("DisplayName", out var displayName) && displayName.ValueKind == JsonValueKind.String
                ? displayName.GetString()!
                : id;
            return new InstalledGame(GameStore.Epic, id, name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static bool IsTrue(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool IsFalse(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.False;
}
