using LeverDeck.Application.Events;
using LeverDeck.Domain.Pairing;
using LeverDeck.Infrastructure.Common;
using LeverDeck.Infrastructure.Pairing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeverDeck.Infrastructure.Tests.Common;

public sealed class InMemoryPairingSessionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_IsNullUntilASessionIsStored()
    {
        Assert.Null(new InMemoryPairingSessionStore().Current);
    }

    [Fact]
    public void Replace_DiscardsThePreviousSession()
    {
        var store = new InMemoryPairingSessionStore();
        store.Replace(PairingSession.Start(new PairingCode("111111"), Now));
        var second = PairingSession.Start(new PairingCode("222222"), Now);

        store.Replace(second);

        Assert.Same(second, store.Current);
    }

    [Fact]
    public void Clear_RemovesTheSession()
    {
        var store = new InMemoryPairingSessionStore();
        store.Replace(PairingSession.Start(new PairingCode("111111"), Now));

        store.Clear();

        Assert.Null(store.Current);
    }
}

public sealed class SystemClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void UtcNow_ComesFromTheTimeProvider()
    {
        var now = new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

        Assert.Equal(now, new SystemClock(new FixedTimeProvider(now)).UtcNow);
    }
}

public sealed class InMemoryEventBroadcasterTests
{
    private readonly InMemoryEventBroadcaster _broadcaster = new(NullLogger<InMemoryEventBroadcaster>.Instance);

    [Fact]
    public async Task Publish_ReachesEverySubscriber()
    {
        var first = new List<ApplicationEvent>();
        var second = new List<ApplicationEvent>();
        using var a = _broadcaster.Subscribe((e, _) => Record(first, e));
        using var b = _broadcaster.Subscribe((e, _) => Record(second, e));

        await _broadcaster.PublishAsync(new SettingsUpdated(), CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task DisposedSubscription_StopsReceiving()
    {
        var received = new List<ApplicationEvent>();
        var subscription = _broadcaster.Subscribe((e, _) => Record(received, e));

        subscription.Dispose();
        await _broadcaster.PublishAsync(new SettingsUpdated(), CancellationToken.None);

        Assert.Empty(received);
    }

    [Fact]
    public async Task FailingSubscriber_DoesNotStopTheOthersOrThePublisher()
    {
        var received = new List<ApplicationEvent>();
        using var failing = _broadcaster.Subscribe((_, _) => throw new InvalidOperationException("boom"));
        using var healthy = _broadcaster.Subscribe((e, _) => Record(received, e));

        await _broadcaster.PublishAsync(new DevicesChanged(), CancellationToken.None);

        Assert.Single(received);
    }

    [Fact]
    public async Task Publish_WithoutSubscribers_Completes()
    {
        await _broadcaster.PublishAsync(new PairingSessionChanged(), CancellationToken.None);
    }

    private static Task Record(List<ApplicationEvent> sink, ApplicationEvent applicationEvent)
    {
        lock (sink)
        {
            sink.Add(applicationEvent);
        }

        return Task.CompletedTask;
    }
}
