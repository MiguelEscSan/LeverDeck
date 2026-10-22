using LeverDeck.Application.Common.Ports;

namespace LeverDeck.Infrastructure.Common;

/// <summary>The wall clock, through <see cref="TimeProvider"/> so tests can substitute it.</summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
