using System.Net.NetworkInformation;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// Advertises the agent while the host runs and withdraws it on shutdown; when the PC's addresses change, it
/// re-advertises so this PC's <see cref="AgentHostName"/> follows the new IP. Turned off with
/// <c>LeverDeck:Mdns:Enabled=false</c>.
/// mDNS is a convenience: a failure is logged and never stops the agent (devices can still use the IP or USB).
/// </summary>
public sealed partial class ServiceAdvertisementHostedService(
    IServiceAdvertiser advertiser,
    LeverDeckOptions options,
    ILogger<ServiceAdvertisementHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan AddressChangeSettleTime = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _stopping = new();
    private int _readvertiseScheduled;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.MdnsEnabled)
        {
            LogDisabled();
            return;
        }

        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        await TryAsync(() => advertiser.AdvertiseAsync(cancellationToken)).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!options.MdnsEnabled)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        await _stopping.CancelAsync().ConfigureAwait(false);
        await TryAsync(() => advertiser.WithdrawAsync(cancellationToken)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _stopping.Dispose();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        // Address changes come in bursts; coalesce them into one re-advertisement.
        if (Interlocked.Exchange(ref _readvertiseScheduled, 1) == 0)
        {
            _ = ReadvertiseAsync();
        }
    }

    private async Task ReadvertiseAsync()
    {
        try
        {
            await Task.Delay(AddressChangeSettleTime, _stopping.Token).ConfigureAwait(false);
            Volatile.Write(ref _readvertiseScheduled, 0);
            await TryAsync(async () =>
            {
                await advertiser.WithdrawAsync(_stopping.Token).ConfigureAwait(false);
                await advertiser.AdvertiseAsync(_stopping.Token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    private async Task TryAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "mDNS advertisement is disabled by configuration.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "mDNS advertisement failed; devices can still connect by IP or USB.")]
    private partial void LogFailed(Exception exception);
}
