using LeverDeck.Application.Icons.Ports;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemoryIconStore : IIconStore
{
    public Dictionary<IconId, IconImage> Stored { get; } = [];

    public int Writes { get; private set; }

    public Task<IReadOnlyList<IconId>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IconId>>([.. Stored.Keys]);

    public Task<bool> ExistsAsync(IconId icon, CancellationToken cancellationToken) => Task.FromResult(Stored.ContainsKey(icon));

    public Task SaveAsync(IconId icon, IconImage image, CancellationToken cancellationToken)
    {
        Writes++;
        Stored[icon] = image;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(IconId icon, CancellationToken cancellationToken)
    {
        Writes++;
        Stored.Remove(icon);
        return Task.CompletedTask;
    }
}
