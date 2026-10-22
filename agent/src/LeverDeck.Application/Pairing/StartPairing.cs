using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Pairing;

/// <summary>Command (admin scope): starts a new pairing session with a fresh code, replacing any previous one.</summary>
public sealed class StartPairing(IPairingSessionStore store, ITokenGenerator tokens, IClock clock, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            store.Replace(PairingSession.Start(tokens.NewPairingCode(), clock.UtcNow));
            await events.PublishAsync(new PairingSessionChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
