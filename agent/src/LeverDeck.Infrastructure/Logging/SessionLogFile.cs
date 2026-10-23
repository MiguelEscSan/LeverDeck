using System.Globalization;
using System.Text;

namespace LeverDeck.Infrastructure.Logging;

/// <summary>
/// The one log file of this run of the agent, in <c>&lt;DataDirectory&gt;/logs</c> (docs/ARCHITECTURE.md, Logs). Starting
/// it deletes every log of earlier runs, so no previous log is kept (the owner's rule). Within the run the file never
/// grows past its cap: the first line that would cross it is replaced by a notice and everything after is dropped, so
/// the start of the session, where most problems show, is what stays. Thread-safe; every write is flushed at once, so a
/// crash loses nothing that was logged.
/// </summary>
public sealed class SessionLogFile : IDisposable
{
    public const string FileName = "leverdeck.log";

    /// <summary>10 MB: weeks of a tray session at the default level, small enough to open in Notepad.</summary>
    public const long DefaultMaxBytes = 10 * 1024 * 1024;

    private const string LogPattern = "*.log";
    private const long Megabyte = 1024 * 1024;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();
    private readonly FileStream? _stream;
    private readonly byte[] _notice;
    private readonly long _limit;
    private long _written;
    private bool _closed;

    private SessionLogFile(FileStream? stream, long maxBytes, byte[] notice)
    {
        _stream = stream;
        _notice = notice;
        _limit = maxBytes - notice.Length;
    }

    /// <summary>Whether the cap was reached; nothing more is written until the agent starts again.</summary>
    public bool IsFull { get; private set; }

    /// <summary>
    /// Deletes the logs of earlier runs in <paramref name="directory"/> (creating it when missing) and opens a new, empty
    /// <see cref="FileName"/>. A file that cannot be deleted is left alone, and a log that cannot be opened turns into one
    /// that drops every line: logging never stops the agent from starting.
    /// </summary>
    public static SessionLogFile Start(string directory, long maxBytes = DefaultMaxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var notice = Utf8.GetBytes(LimitNotice(maxBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, notice.Length);

        Directory.CreateDirectory(directory);
        foreach (var earlier in Directory.GetFiles(directory, LogPattern))
        {
            TryDelete(earlier);
        }

        FileStream? stream;
        try
        {
            // FileShare.Read: the owner can open the log while the agent runs; another process cannot delete it.
            stream = new FileStream(Path.Combine(directory, FileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            stream = null;
        }

        return new SessionLogFile(stream, maxBytes, notice);
    }

    /// <summary>The last line of a log that reached <paramref name="maxBytes"/>.</summary>
    public static string LimitNotice(long maxBytes)
    {
        var size = maxBytes % Megabyte == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{maxBytes / Megabyte} MB")
            : string.Create(CultureInfo.InvariantCulture, $"{maxBytes} bytes");
        return $"The log reached its {size} limit; later messages are dropped until LeverDeck restarts.{Environment.NewLine}";
    }

    /// <summary>Appends <paramref name="text"/> as it is (the caller ends its lines), or the limit notice at the cap.</summary>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Utf8.GetBytes(text);
        lock (_gate)
        {
            if (_stream is null || _closed || IsFull)
            {
                return;
            }

            if (_written + bytes.Length > _limit)
            {
                IsFull = true;
                Append(_notice);
                return;
            }

            _written += bytes.Length;
            Append(bytes);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _stream?.Dispose();
        }
    }

    private void Append(byte[] bytes)
    {
        try
        {
            _stream!.Write(bytes);
            _stream.Flush();
        }
        catch (IOException)
        {
            // The disk is full or gone: stop writing rather than fail the caller, which is only logging.
            _closed = true;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Still open elsewhere (an editor, another agent that is shutting down): the next start deletes it.
        }
    }
}
