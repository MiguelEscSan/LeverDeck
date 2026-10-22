using LeverDeck.Application.Common.Ports;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// Runs several advertisers as one: the host-name responder (A record) and the native service registration (SRV).
/// Every advertiser is called even when an earlier one fails; the failures are then thrown together.
/// </summary>
public sealed class CompositeServiceAdvertiser(IReadOnlyList<IServiceAdvertiser> advertisers) : IServiceAdvertiser
{
    public IReadOnlyList<IServiceAdvertiser> Advertisers { get; } = advertisers ?? throw new ArgumentNullException(nameof(advertisers));

    public Task AdvertiseAsync(CancellationToken cancellationToken) =>
        RunAllAsync(advertiser => advertiser.AdvertiseAsync(cancellationToken));

    public Task WithdrawAsync(CancellationToken cancellationToken) =>
        RunAllAsync(advertiser => advertiser.WithdrawAsync(cancellationToken));

    private async Task RunAllAsync(Func<IServiceAdvertiser, Task> operation)
    {
        List<Exception>? failures = null;
        foreach (var advertiser in Advertisers)
        {
            try
            {
                await operation(advertiser).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
