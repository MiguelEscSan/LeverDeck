using LeverDeck.Application.Events;

namespace LeverDeck.Application.Ports;

/// <summary>Pushes application events to the connected clients.</summary>
public interface IEventPublisher
{
    Task PublishAsync(ApplicationEvent applicationEvent, CancellationToken cancellationToken);
}
