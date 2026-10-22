namespace LeverDeck.Application.Common.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
