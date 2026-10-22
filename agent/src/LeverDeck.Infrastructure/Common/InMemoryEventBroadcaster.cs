using System.Collections.Immutable;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Common;

/// <summary>
/// Fans every application event out to the current subscribers, in subscription order. Transports (Phase 6) subscribe
/// to push events to their connections. A failing subscriber is logged and skipped: a change that already succeeded
/// never turns into a failed use case because one connection could not be notified.
/// </summary>
public sealed partial class InMemoryEventBroadcaster(ILogger<InMemoryEventBroadcaster> logger) : IEventPublisher
{
    private ImmutableArray<Func<ApplicationEvent, CancellationToken, Task>> _subscribers = [];

    /// <summary>Adds a subscriber; dispose the returned handle to remove it.</summary>
    public IDisposable Subscribe(Func<ApplicationEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ImmutableInterlocked.Update(ref _subscribers, subscribers => subscribers.Add(handler));
        return new Subscription(this, handler);
    }

    public async Task PublishAsync(ApplicationEvent applicationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);
        foreach (var subscriber in _subscribers)
        {
            try
            {
                await subscriber(applicationEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSubscriberFailed(exception, applicationEvent.GetType().Name);
            }
        }
    }

    private void Unsubscribe(Func<ApplicationEvent, CancellationToken, Task> handler) =>
        ImmutableInterlocked.Update(ref _subscribers, subscribers => subscribers.Remove(handler));

    [LoggerMessage(Level = LogLevel.Warning, Message = "An event subscriber failed to handle {EventType}.")]
    private partial void LogSubscriberFailed(Exception exception, string eventType);

    private sealed class Subscription(InMemoryEventBroadcaster owner, Func<ApplicationEvent, CancellationToken, Task> handler)
        : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unsubscribe(handler);
            }
        }
    }
}
