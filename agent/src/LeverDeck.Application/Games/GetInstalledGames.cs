using LeverDeck.Application.Common;
using LeverDeck.Application.Games.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Games;

/// <summary>
/// Query (admin scope): the installed games the editor can put on a <see cref="LaunchGame"/> key. Only games whose id
/// a <see cref="LaunchGame"/> accepts are listed, once each, by name; a game without a name shows its id.
/// </summary>
public sealed class GetInstalledGames(IGameLibrary library)
{
    public Task<Result<IReadOnlyList<InstalledGame>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var found = await library.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<InstalledGame> games =
            [
                .. found
                    .Where(game => LaunchGame.IsValidId(game.Store, game.GameId))
                    .DistinctBy(game => (game.Store, game.GameId))
                    .Select(game => game with { Name = string.IsNullOrWhiteSpace(game.Name) ? game.GameId : game.Name.Trim() })
                    .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Store),
            ];
            return Result.Success(games);
        });
}
