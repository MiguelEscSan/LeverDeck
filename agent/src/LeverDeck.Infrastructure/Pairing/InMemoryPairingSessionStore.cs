using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Infrastructure.Pairing;

/// <summary>Holds the single current pairing session in memory; it is lost on restart by design.</summary>
public sealed class InMemoryPairingSessionStore : IPairingSessionStore
{
    private PairingSession? _current;

    public PairingSession? Current => Volatile.Read(ref _current);

    public void Replace(PairingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Volatile.Write(ref _current, session);
    }

    public void Clear() => Volatile.Write(ref _current, null);
}
