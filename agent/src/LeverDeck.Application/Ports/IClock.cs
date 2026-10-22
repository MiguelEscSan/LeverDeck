namespace LeverDeck.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
