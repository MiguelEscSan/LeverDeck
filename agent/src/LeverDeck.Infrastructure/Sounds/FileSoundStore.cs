using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;
using LeverDeck.Infrastructure.Persistence.Records;

namespace LeverDeck.Infrastructure.Sounds;

/// <summary>
/// Uploaded sound clips: the library in <c>sounds.json</c> and each clip's WAV file as <c>sounds/&lt;id&gt;.wav</c> in
/// the data directory. Files are written atomically, and only ids of the <see cref="SoundId"/> shape ever name a file.
/// </summary>
public sealed class FileSoundStore : ISoundStore, IDisposable
{
    public const string FolderName = "sounds";
    public const string IndexFileName = "sounds.json";
    public const string Extension = ".wav";

    private readonly JsonDocumentFile<SoundsDocument> _index;

    public FileSoundStore(LeverDeckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Folder = Path.Combine(options.DataDirectory, FolderName);
        _index = new JsonDocumentFile<SoundsDocument>(
            Path.Combine(options.DataDirectory, IndexFileName),
            PersistenceJsonContext.Default.SoundsDocument,
            document => document.SchemaVersion,
            SoundsDocument.CurrentVersion);
    }

    public string Folder { get; }

    /// <summary>The WAV file of a clip.</summary>
    public string PathOf(SoundId sound) => Path.Combine(Folder, sound.Value + Extension);

    public Task<IReadOnlyList<SoundClip>> ListAsync(CancellationToken cancellationToken) =>
        _index.ReadAsync(
            document => document is null ? [] : (IReadOnlyList<SoundClip>)[.. document.Sounds.Select(ToDomain)],
            cancellationToken);

    public Task<SoundClip?> FindAsync(SoundId sound, CancellationToken cancellationToken) =>
        _index.ReadAsync(
            document => document?.Sounds.FirstOrDefault(record => record.Id == sound.Value) is { } record ? ToDomain(record) : null,
            cancellationToken);

    public async Task SaveAsync(SoundClip clip, ReadOnlyMemory<byte> wave, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clip);
        Directory.CreateDirectory(Folder);
        await AtomicFile.WriteAllBytesAsync(PathOf(clip.Id), wave.ToArray(), cancellationToken).ConfigureAwait(false);
        await _index.UpdateAsync(document => WithSounds(document, sounds => [.. sounds, ToRecord(clip)]), cancellationToken).ConfigureAwait(false);
    }

    public Task RenameAsync(SoundId sound, SoundName name, CancellationToken cancellationToken) =>
        _index.UpdateAsync(
            document => WithSounds(document, sounds => [.. sounds.Select(record => record.Id == sound.Value ? record with { Name = name.Value } : record)]),
            cancellationToken);

    public async Task DeleteAsync(SoundId sound, CancellationToken cancellationToken)
    {
        await _index.UpdateAsync(
            document => WithSounds(document, sounds => [.. sounds.Where(record => record.Id != sound.Value)]),
            cancellationToken).ConfigureAwait(false);
        File.Delete(PathOf(sound));
    }

    public void Dispose() => _index.Dispose();

    // A missing library reads as an empty one: a rename or delete against it changes nothing, and the write is harmless.
    private static SoundsDocument WithSounds(SoundsDocument? document, Func<IEnumerable<SoundRecord>, List<SoundRecord>> change) =>
        document is null
            ? new SoundsDocument(SoundsDocument.CurrentVersion, change([]))
            : document with { Sounds = change(document.Sounds) };

    private static SoundClip ToDomain(SoundRecord record) =>
        new(new SoundId(record.Id), new SoundName(record.Name), record.DurationMs, record.SizeBytes);

    private static SoundRecord ToRecord(SoundClip clip) =>
        new(clip.Id.Value, clip.Name.Value, clip.DurationMs, clip.SizeBytes);
}
