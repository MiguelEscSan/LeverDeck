using LeverDeck.Application.Games;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;

namespace LeverDeck.Application.Tests.Games;

public sealed class GetInstalledGamesTests
{
    private readonly Harness _harness = new();

    [Fact]
    public async Task ExecuteAsync_ListsTheLibrarysGamesByName()
    {
        _harness.Games.Games.AddRange(
        [
            new InstalledGame(GameStore.Steam, "570", "Dota 2"),
            new InstalledGame(GameStore.Epic, "Fortnite", "Fortnite"),
            new InstalledGame(GameStore.Steam, "730", "Counter-Strike 2"),
        ]);

        var result = await _harness.GetInstalledGames().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal(["Counter-Strike 2", "Dota 2", "Fortnite"], result.Value.Select(game => game.Name));
    }

    [Fact]
    public async Task ExecuteAsync_DropsIdsALaunchGameWouldRefuse()
    {
        _harness.Games.Games.AddRange(
        [
            new InstalledGame(GameStore.Steam, "570", "Dota 2"),
            new InstalledGame(GameStore.Steam, "not-a-number", "Broken"),
            new InstalledGame(GameStore.Epic, "has:colons", "Broken too"),
        ]);

        var result = await _harness.GetInstalledGames().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal([new InstalledGame(GameStore.Steam, "570", "Dota 2")], result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ListsEachGameOnceAndNamesNamelessOnesByTheirId()
    {
        _harness.Games.Games.AddRange(
        [
            new InstalledGame(GameStore.Steam, "570", " Dota 2 "),
            new InstalledGame(GameStore.Steam, "570", "Dota 2 (second library)"),
            new InstalledGame(GameStore.Epic, "Sugar", "  "),
        ]);

        var result = await _harness.GetInstalledGames().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal(
            [new InstalledGame(GameStore.Steam, "570", "Dota 2"), new InstalledGame(GameStore.Epic, "Sugar", "Sugar")],
            result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ChangesNothing()
    {
        _harness.Games.Games.Add(new InstalledGame(GameStore.Steam, "570", "Dota 2"));

        await _harness.GetInstalledGames().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }
}
