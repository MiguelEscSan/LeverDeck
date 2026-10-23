using LeverDeck.Infrastructure.Logging;
using LeverDeck.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LeverDeck.App.Tests;

/// <summary>Release has no console (WinExe), so it logs to the data directory; Debug keeps the console.</summary>
public sealed class LoggingTests
{
    [Fact]
    public void Startup_LogsToTheDataDirectoryOnlyInReleaseBuilds()
    {
        using var factory = new LeverDeckAppFactory();
        var logs = Path.Combine(factory.DataDirectory, "logs");
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "older.log"), "a previous run");

        var providers = factory.Services.GetServices<ILoggerProvider>().ToList();

#if DEBUG
        Assert.DoesNotContain(providers, provider => provider is FileLoggerProvider);
        Assert.True(File.Exists(Path.Combine(logs, "older.log")));
#else
        Assert.Single(providers, provider => provider is FileLoggerProvider);
        Assert.Equal([Path.Combine(logs, SessionLogFile.FileName)], Directory.GetFiles(logs));
        var log = ReadShared(Path.Combine(logs, SessionLogFile.FileName));
        Assert.Contains("Desktop editor", log, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Services.GetRequiredService<AdminTokenStore>().GetOrCreate(), log, StringComparison.Ordinal);
#endif
    }

#if !DEBUG
    private static string ReadShared(string path)
    {
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd();
    }
#endif
}
