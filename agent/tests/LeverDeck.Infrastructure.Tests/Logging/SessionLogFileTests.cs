using System.Text;
using LeverDeck.Infrastructure.Logging;

namespace LeverDeck.Infrastructure.Tests.Logging;

public sealed class SessionLogFileTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private string LogPath => _directory.File(SessionLogFile.FileName);

    private string ReadLog() => File.ReadAllText(LogPath, Encoding.UTF8);

    [Fact]
    public void Start_DeletesEveryLogOfEarlierRunsAndBeginsAnEmptyOne()
    {
        File.WriteAllText(LogPath, "the last session");
        File.WriteAllText(_directory.File("leverdeck-20261001.log"), "an older session");

        using (SessionLogFile.Start(_directory.Path))
        {
        }

        Assert.Equal([LogPath], Directory.GetFiles(_directory.Path));
        Assert.Equal(string.Empty, ReadLog());
    }

    [Fact]
    public void Start_LeavesFilesThatAreNotLogs()
    {
        File.WriteAllText(_directory.File("notes.txt"), "kept");

        using (SessionLogFile.Start(_directory.Path))
        {
        }

        Assert.True(File.Exists(_directory.File("notes.txt")));
    }

    [Fact]
    public void Start_CreatesTheDirectoryWhenMissing()
    {
        var folder = Path.Combine(_directory.Path, "logs");

        using (SessionLogFile.Start(folder))
        {
        }

        Assert.True(File.Exists(Path.Combine(folder, SessionLogFile.FileName)));
    }

    [Fact]
    public void Write_AppendsEachLineAndCanBeReadWhileOpen()
    {
        using var log = SessionLogFile.Start(_directory.Path);

        log.Write("first\n");
        log.Write("second ñ\n");

        using var reader = new StreamReader(new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.Equal("first\nsecond ñ\n", reader.ReadToEnd());
        Assert.False(log.IsFull);
    }

    [Fact]
    public void Write_AtTheCap_StopsWritingAndSaysSoOnce()
    {
        const long cap = 400;
        var line = new string('x', 49) + "\n";

        using (var log = SessionLogFile.Start(_directory.Path, cap))
        {
            for (var i = 0; i < 20; i++)
            {
                log.Write(line);
            }

            Assert.True(log.IsFull);
        }

        var text = ReadLog();
        Assert.True(new FileInfo(LogPath).Length <= cap);
        Assert.StartsWith(line, text, StringComparison.Ordinal);
        Assert.EndsWith(SessionLogFile.LimitNotice(cap), text, StringComparison.Ordinal);
        Assert.Equal(text.IndexOf("limit", StringComparison.Ordinal), text.LastIndexOf("limit", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_AfterTheCap_DropsEvenShortLines()
    {
        const long cap = 300;

        using (var log = SessionLogFile.Start(_directory.Path, cap))
        {
            log.Write(new string('x', 299) + "\n");
            log.Write("a\n");
        }

        Assert.Equal(SessionLogFile.LimitNotice(cap), ReadLog());
    }

    [Fact]
    public void LimitNotice_NamesTheCapInMegabytesAndTheWayToStartAgain()
    {
        var notice = SessionLogFile.LimitNotice(SessionLogFile.DefaultMaxBytes);

        Assert.Contains("10 MB", notice, StringComparison.Ordinal);
        Assert.Contains("restart", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_RefusesACapTooSmallForTheNotice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionLogFile.Start(_directory.Path, maxBytes: 10));
    }

    [Fact]
    public void Write_AfterDispose_IsIgnored()
    {
        var log = SessionLogFile.Start(_directory.Path);
        log.Dispose();

        log.Write("late\n");

        Assert.Equal(string.Empty, ReadLog());
    }
}
