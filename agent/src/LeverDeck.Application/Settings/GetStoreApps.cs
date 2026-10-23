using LeverDeck.Application.Common;
using LeverDeck.Application.Security;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Settings;

/// <summary>
/// Query (admin scope): the Store apps the editor can register. Only AppUserModelIds that <see cref="RegisteredApp"/>
/// accepts are listed, once each, by name; an app without a name shows its id.
/// </summary>
public sealed class GetStoreApps(IStoreAppCatalog catalog)
{
    public Task<Result<IReadOnlyList<StoreApp>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var found = await catalog.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<StoreApp> apps =
            [
                .. found
                    .Where(app => RegisteredApp.IsValidAppUserModelId(app.AppUserModelId))
                    .DistinctBy(app => app.AppUserModelId)
                    .Select(app => app with { Name = string.IsNullOrWhiteSpace(app.Name) ? app.AppUserModelId : app.Name.Trim() })
                    .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
            ];
            return Result.Success(apps);
        });
}
