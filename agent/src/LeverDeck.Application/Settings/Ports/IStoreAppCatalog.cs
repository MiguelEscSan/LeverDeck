namespace LeverDeck.Application.Settings.Ports;

/// <summary>
/// Lists the packaged (Microsoft Store) apps in the Start menu. Read-only: it never starts or changes anything. A catalog
/// that cannot be read contributes no apps rather than failing.
/// </summary>
public interface IStoreAppCatalog
{
    Task<IReadOnlyList<StoreApp>> ListInstalledAsync(CancellationToken cancellationToken);
}
