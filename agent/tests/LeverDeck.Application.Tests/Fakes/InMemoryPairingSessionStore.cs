using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemoryPairingSessionStore : IPairingSessionStore
{
    public PairingSession? Current { get; private set; }

    public void Replace(PairingSession session) => Current = session;

    public void Clear() => Current = null;
}
