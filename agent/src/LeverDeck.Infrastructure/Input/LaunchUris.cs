using LeverDeck.Domain.Actions;

namespace LeverDeck.Infrastructure.Input;

/// <summary>
/// The store URIs that launch a game. The id was validated by <see cref="LaunchGame"/> (digits for Steam, letters and
/// digits for Epic), so nothing else can end up in the URI.
/// </summary>
internal static class LaunchUris
{
    public static Uri For(LaunchGame game) => game.Store switch
    {
        GameStore.Steam => new Uri($"steam://rungameid/{game.GameId}"),
        GameStore.Epic => new Uri($"com.epicgames.launcher://apps/{game.GameId}?action=launch&silent=true"),
        _ => throw new ArgumentOutOfRangeException(nameof(game), game.Store, "Unknown game store."),
    };
}
