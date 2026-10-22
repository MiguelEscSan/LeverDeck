using LeverDeck.Application.Actions.Ports;
using LeverDeck.Application.Games;
using LeverDeck.Application.Games.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using LeverDeck.Domain.Actions;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.App.Tests.Transport;

/// <summary>
/// The isolated host with the real transport. The action executor is always replaced by a recorder, so a test that
/// presses a key never injects real input, and the game library by a fixed list, so no test reads the PC's launchers.
/// The connect timeout is generous, so a slow CI runner never times out a test that does send its first message; the
/// test of the timeout itself passes <see cref="ShortConnectTimeout"/> to stay fast.
/// </summary>
public sealed class TransportAppFactory(TimeSpan? connectTimeout = null) : LeverDeckAppFactory
{
    public static readonly TimeSpan ShortConnectTimeout = TimeSpan.FromMilliseconds(500);

    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

    public TimeSpan ConnectTimeout { get; } = connectTimeout ?? DefaultConnectTimeout;

    public RecordingActionExecutor Executor { get; } = new();

    public MutableTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public FixedGameLibrary Games { get; } = new([new InstalledGame(GameStore.Steam, "570", "Dota 2")]);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(LeverDeckOptions.ConnectTimeoutKey, ConnectTimeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IActionExecutor>(Executor);
            services.AddSingleton<IGameLibrary>(Games);
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

/// <summary>Always reports the same installed games.</summary>
public sealed class FixedGameLibrary(IReadOnlyList<InstalledGame> games) : IGameLibrary
{
    public Task<IReadOnlyList<InstalledGame>> ListInstalledAsync(CancellationToken cancellationToken) => Task.FromResult(games);
}
