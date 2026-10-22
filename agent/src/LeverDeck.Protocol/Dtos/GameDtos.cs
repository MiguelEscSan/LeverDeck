namespace LeverDeck.Protocol.Dtos;

/// <summary>A game installed on the PC, as the editor offers it for a <c>launchGame</c> key.</summary>
public sealed record InstalledGameDto(GameStoreDto Store, string GameId, string Name);
