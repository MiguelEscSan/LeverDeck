using LeverDeck.Domain.Actions;

namespace LeverDeck.Application.Games;

/// <summary>A game a store reports as installed on the PC: its store, its launch id and the name to show.</summary>
public sealed record InstalledGame(GameStore Store, string GameId, string Name);
