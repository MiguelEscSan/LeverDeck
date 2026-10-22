using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>
/// One JSON file with one lock: every read and read-modify-write runs alone, and writes are atomic
/// (<see cref="AtomicFile"/>). A missing file reads as <see langword="null"/>. A file that cannot be parsed, has an
/// unknown schema version or breaks a domain rule throws <see cref="InvalidDataException"/> and is never overwritten,
/// so user data is not silently replaced by defaults.
/// </summary>
internal sealed class JsonDocumentFile<TDocument>(string path, JsonTypeInfo<TDocument> typeInfo, Func<TDocument, int> schemaVersion, int currentVersion)
    : IDisposable
    where TDocument : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Path { get; } = path;

    /// <summary>
    /// Loads the document, runs <paramref name="body"/> on it and writes the document the body returns, if any.
    /// </summary>
    public async Task<TResult> TransactAsync<TResult>(
        Func<TDocument?, (TDocument? Write, TResult Result)> body,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var (write, result) = Guard(() => body(current));
            if (write is not null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(write, typeInfo);
                await AtomicFile.WriteAllBytesAsync(Path, bytes, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    public Task<TResult> ReadAsync<TResult>(Func<TDocument?, TResult> read, CancellationToken cancellationToken) =>
        TransactAsync(current => ((TDocument?)null, read(current)), cancellationToken);

    public Task UpdateAsync(Func<TDocument?, TDocument> change, CancellationToken cancellationToken) =>
        TransactAsync(current => ((TDocument?)change(current), true), cancellationToken);

    private async Task<TDocument?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(Path, cancellationToken).ConfigureAwait(false);
        TDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, typeInfo);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw Corrupt(exception.Message, exception);
        }

        if (document is null)
        {
            throw Corrupt("the file holds no document.", null);
        }

        var version = schemaVersion(document);
        if (version != currentVersion)
        {
            throw Corrupt($"schema version {version} is not supported (expected {currentVersion}).", null);
        }

        return document;
    }

    // Mapping stored records back to the domain re-validates every invariant; a broken rule means a corrupt file.
    private T Guard<T>(Func<T> map)
    {
        try
        {
            return map();
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException or FormatException or CryptographicException)
        {
            throw Corrupt(exception.Message, exception);
        }
    }

    private InvalidDataException Corrupt(string reason, Exception? inner) =>
        new($"{Path} is invalid and was left untouched: {reason}", inner);
}
