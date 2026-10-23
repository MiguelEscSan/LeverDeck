using LeverDeck.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Tests.Discovery;

internal static class AgentHostNames
{
    public static AgentHostName Of(string text) =>
        AgentHostName.TryParse(text, out var name) ? name! : throw new ArgumentException($"'{text}' is not a host name.", nameof(text));
}

/// <summary>Keeps the formatted messages a component logs, so a test can wait for one.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    public async Task<string> WaitForAsync(LogLevel level)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (_entries)
            {
                foreach (var (entryLevel, message) in _entries)
                {
                    if (entryLevel == level)
                    {
                        return message;
                    }
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Nothing was logged at {level}.");
    }
}
