using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<ApplicationEvent> _published = [];

    public IReadOnlyList<ApplicationEvent> Published => _published;

    public Task PublishAsync(ApplicationEvent applicationEvent, CancellationToken cancellationToken)
    {
        _published.Add(applicationEvent);
        return Task.CompletedTask;
    }
}
