using LeverDeck.Application.Common;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Pairing;

/// <summary>
/// Query (admin scope): the session a device can still redeem, or <see langword="null"/> when there is none or it is
/// expired, exhausted or redeemed. The QR URL is built by the transport (Phase 3).
/// </summary>
public sealed class GetPairingSession(IPairingSessionStore store, IClock clock)
{
    public Task<Result<PairingSession?>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, () =>
        {
            var session = store.Current;
            var active = session is not null && session.IsActive(clock.UtcNow) ? session : null;
            return Task.FromResult(Result.Success(active));
        });
}
