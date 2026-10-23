using System.Text;
using LeverDeck.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Tests.Logging;

public sealed class FileLoggerProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 13, 45, 7, 123, TimeSpan.Zero);

    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private string WriteThroughALogger(Action<ILogger> log)
    {
        using (var provider = new FileLoggerProvider(SessionLogFile.Start(_directory.Path), new FixedTimeProvider(Now)))
        {
            log(provider.CreateLogger("LeverDeck.App.Tray.TrayIcon"));
        }

        return File.ReadAllText(_directory.File(SessionLogFile.FileName), Encoding.UTF8);
    }

    [Fact]
    public void Log_WritesTheUtcTimeTheLevelTheCategoryAndTheMessageOnOneLine()
    {
        var text = WriteThroughALogger(logger => Log(logger, LogLevel.Warning, "Port 8765 is busy"));

        Assert.Equal("2026-10-23T13:45:07.123Z warn LeverDeck.App.Tray.TrayIcon: Port 8765 is busy" + Environment.NewLine, text);
    }

    [Fact]
    public void Log_WritesTheExceptionAfterTheMessage()
    {
        var text = WriteThroughALogger(logger => Log(logger, LogLevel.Error, "Failed", new InvalidOperationException("boom")));

        Assert.StartsWith("2026-10-23T13:45:07.123Z fail LeverDeck.App.Tray.TrayIcon: Failed" + Environment.NewLine, text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: boom", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "trce")]
    [InlineData(LogLevel.Debug, "dbug")]
    [InlineData(LogLevel.Information, "info")]
    [InlineData(LogLevel.Critical, "crit")]
    public void Log_NamesTheLevelLikeTheConsole(LogLevel level, string name)
    {
        var text = WriteThroughALogger(logger => Log(logger, level, "message"));

        Assert.Contains($" {name} ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IsEnabled_IsFalseOnlyForNone()
    {
        using var provider = new FileLoggerProvider(SessionLogFile.Start(_directory.Path), TimeProvider.System);
        var logger = provider.CreateLogger("category");

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    // The raw ILogger.Log call, as the generated LoggerMessage methods make it.
    private static void Log(ILogger logger, LogLevel level, string message, Exception? exception = null) =>
        logger.Log(level, default, message, exception, static (state, _) => state);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
