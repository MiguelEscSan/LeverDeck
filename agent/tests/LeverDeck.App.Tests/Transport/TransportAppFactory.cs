using LeverDeck.Application.Actions.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.App.Tests.Transport;

/// <summary>
/// The isolated host with the real transport. The action executor is always replaced by a recorder, so a test that
/// presses a key never injects real input; the connect timeout is short so the timeout tests stay fast.
/// </summary>
public sealed class TransportAppFactory : LeverDeckAppFactory
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

    public RecordingActionExecutor Executor { get; } = new();

    public MutableTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(LeverDeckOptions.ConnectTimeoutKey, ConnectTimeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IActionExecutor>(Executor);
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}

/// <summary>Records every action instead of running it.</summary>
public sealed class RecordingActionExecutor : IActionExecutor
{
    private readonly List<DomainAction> _executed = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<DomainAction> Executed
    {
        get
        {
            lock (_gate)
            {
                return [.. _executed];
            }
        }
    }

    public Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _executed.Add(action);
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>A clock the test moves by hand; only the agent's <c>IClock</c> reads it.</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
}
