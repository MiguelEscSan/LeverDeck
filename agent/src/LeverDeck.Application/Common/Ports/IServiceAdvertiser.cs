namespace LeverDeck.Application.Common.Ports;

/// <summary>Advertises the agent as <c>leverdeck.local</c> on the LAN. Used from Phase 4; no use case needs it yet.</summary>
public interface IServiceAdvertiser
{
    Task AdvertiseAsync(CancellationToken cancellationToken);

    Task WithdrawAsync(CancellationToken cancellationToken);
}
