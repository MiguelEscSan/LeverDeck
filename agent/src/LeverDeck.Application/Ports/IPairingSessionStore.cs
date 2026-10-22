using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Ports;

/// <summary>Holds the single current pairing session in memory; it is never persisted.</summary>
public interface IPairingSessionStore
{
    /// <summary>The current session, which may be expired, exhausted or redeemed; <see langword="null"/> if none.</summary>
    PairingSession? Current { get; }

    /// <summary>Makes <paramref name="session"/> the current one, discarding any previous session.</summary>
    void Replace(PairingSession session);

    void Clear();
}
