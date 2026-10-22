namespace LeverDeck.Application.Games.Ports;

/// <summary>
/// Reads which games the store launchers (Steam, Epic) have installed. Read-only: it never starts or changes anything.
/// A store that is not installed, or whose data cannot be read, contributes no games rather than failing.
/// </summary>
public interface IGameLibrary
{
    Task<IReadOnlyList<InstalledGame>> ListInstalledAsync(CancellationToken cancellationToken);
}
