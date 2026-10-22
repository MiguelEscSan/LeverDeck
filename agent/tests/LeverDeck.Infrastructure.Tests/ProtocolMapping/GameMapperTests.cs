using LeverDeck.Application.Games;
using LeverDeck.Domain.Actions;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class GameMapperTests
{
    [Theory]
    [InlineData(GameStore.Steam, GameStoreDto.Steam)]
    [InlineData(GameStore.Epic, GameStoreDto.Epic)]
    public void ToDto_MapsEveryStore(GameStore store, GameStoreDto expected)
    {
        Assert.Equal(new InstalledGameDto(expected, "1", "Game"), GameMapper.ToDto(new InstalledGame(store, "1", "Game")));
    }
}
