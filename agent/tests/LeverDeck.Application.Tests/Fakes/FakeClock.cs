using LeverDeck.Application.Ports;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public static DateTimeOffset Start { get; } = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; } = Start;

    public void Advance(TimeSpan by) => UtcNow += by;
}
