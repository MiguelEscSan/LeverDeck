using LeverDeck.Application.Games;
using LeverDeck.Application.Games.Ports;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class FakeGameLibrary : IGameLibrary
{
    public List<InstalledGame> Games { get; } = [];

    public int Reads { get; private set; }

    public Task<IReadOnlyList<InstalledGame>> ListInstalledAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<InstalledGame>>([.. Games]);
    }
}
