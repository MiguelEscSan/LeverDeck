namespace LeverDeck.Infrastructure.Persistence;

/// <summary>
/// Writes a file so that a crash leaves either the old or the new content, never a half-written file: the bytes go to
/// a sibling temp file, are flushed to disk, and the temp file then replaces the target in one rename.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }

    public static void WriteAllBytes(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
