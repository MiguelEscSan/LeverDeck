using LeverDeck.Application.Games;
using LeverDeck.Domain.Actions;
using LeverDeck.Protocol.Dtos;
using static LeverDeck.Infrastructure.ProtocolMapping.EnumMapping;

namespace LeverDeck.Infrastructure.ProtocolMapping;

public static class GameMapper
{
    public static InstalledGameDto ToDto(InstalledGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new InstalledGameDto(Convert<GameStore, GameStoreDto>(game.Store), game.GameId, game.Name);
    }
}
