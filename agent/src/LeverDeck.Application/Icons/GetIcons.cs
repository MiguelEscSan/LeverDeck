using LeverDeck.Application.Common;
using LeverDeck.Application.Icons.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Icons;

/// <summary>Query (admin scope): the uploaded icons, by id.</summary>
public sealed class GetIcons(IIconStore icons)
{
    public Task<Result<IReadOnlyList<IconId>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var stored = await icons.ListAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<IconId> sorted = [.. stored.OrderBy(icon => icon.Value, StringComparer.Ordinal)];
            return Result.Success(sorted);
        });
}
