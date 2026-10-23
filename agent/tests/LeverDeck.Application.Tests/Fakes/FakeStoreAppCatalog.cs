using LeverDeck.Application.Settings;
using LeverDeck.Application.Settings.Ports;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class FakeStoreAppCatalog : IStoreAppCatalog
{
    public List<StoreApp> Apps { get; } = [];

    public Task<IReadOnlyList<StoreApp>> ListInstalledAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoreApp>>(Apps);
}
