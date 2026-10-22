using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>
/// Launches a game through its store. A Steam id is 1–10 ASCII digits; an Epic id is 1–64 ASCII letters or digits.
/// </summary>
public sealed record LaunchGame : Action
{
    public const int MaxSteamIdLength = 10;

    public const int MaxEpicIdLength = 64;

    public LaunchGame(GameStore store, string gameId)
    {
        Invariant.RequireDefined(store, "game store");
        Invariant.Require(IsValidId(store, gameId), DomainErrorCode.ValidationFailed, $"Game id is not a valid {store} id.");

        Store = store;
        GameId = gameId;
    }

    public GameStore Store { get; }

    public string GameId { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;

    /// <summary>Whether <paramref name="gameId"/> is a valid id for <paramref name="store"/>, as the constructor requires.</summary>
    public static bool IsValidId(GameStore store, string? gameId) => store switch
    {
        GameStore.Steam => gameId is { Length: > 0 and <= MaxSteamIdLength } && gameId.All(char.IsAsciiDigit),
        GameStore.Epic => gameId is { Length: > 0 and <= MaxEpicIdLength } && gameId.All(char.IsAsciiLetterOrDigit),
        _ => false,
    };
}
