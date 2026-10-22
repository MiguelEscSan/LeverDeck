using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Pairing;

/// <summary>Command (admin scope): ends the current pairing session. Without one it is a no-op that still acks.</summary>
public sealed class CancelPairing(IPairingSessionStore store, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            store.Clear();
            await events.PublishAsync(new PairingSessionChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
