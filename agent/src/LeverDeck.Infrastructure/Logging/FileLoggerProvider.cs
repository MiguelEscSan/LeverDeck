using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Logging;

/// <summary>
/// Writes log entries to the run's <see cref="SessionLogFile"/>, one line each: UTC time, the console's four-letter
/// level, the category and the message, then the exception if there is one. No reflection and no package beyond the
/// logging abstractions, so it is Native AOT safe. Levels are filtered by the <c>Logging</c> configuration as for any
/// provider.
/// </summary>
public sealed class FileLoggerProvider(SessionLogFile file, TimeProvider time) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() => file.Dispose();

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var line = new StringBuilder(64 + category.Length + message.Length)
            .Append(time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(LevelName(level))
            .Append(' ')
            .Append(category)
            .Append(": ")
            .Append(message)
            .Append(Environment.NewLine);
        if (exception is not null)
        {
            line.Append(exception).Append(Environment.NewLine);
        }

        file.Write(line.ToString());
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "none",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
