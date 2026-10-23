using LeverDeck.Domain.Sounds;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Sounds;

namespace LeverDeck.Infrastructure.Tests.Sounds;

public sealed class FileSoundStoreTests : IDisposable
{
    private static readonly SoundId _clip = new("sound-0123456789abcdef0123456789abcdef");
    private static readonly byte[] _wave = [1, 2, 3, 4];

    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private FileSoundStore NewStore() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    [Fact]
    public async Task SaveThenList_KeepsTheClipAndItsFileAcrossInstances()
    {
        using (var store = NewStore())
        {
            await store.SaveAsync(new SoundClip(_clip, new SoundName("Applause"), 3000, _wave.Length), _wave, CancellationToken.None);
        }

        using var reopened = NewStore();
        var clips = await reopened.ListAsync(CancellationToken.None);

        var clip = Assert.Single(clips);
        Assert.Equal(_clip, clip.Id);
        Assert.Equal("Applause", clip.Name.Value);
        Assert.Equal(3000, clip.DurationMs);
        Assert.Equal(_wave, File.ReadAllBytes(reopened.PathOf(_clip)));
    }

    [Fact]
    public async Task Rename_ChangesOnlyTheNameAndKeepsTheFile()
    {
        using var store = NewStore();
        await store.SaveAsync(new SoundClip(_clip, new SoundName("Applause"), 3000, _wave.Length), _wave, CancellationToken.None);

        await store.RenameAsync(_clip, new SoundName("Crowd"), CancellationToken.None);

        Assert.Equal("Crowd", (await store.FindAsync(_clip, CancellationToken.None))!.Name.Value);
        Assert.True(File.Exists(store.PathOf(_clip)));
    }

    [Fact]
    public async Task Delete_RemovesTheEntryAndTheFile()
    {
        using var store = NewStore();
        await store.SaveAsync(new SoundClip(_clip, new SoundName("Applause"), 3000, _wave.Length), _wave, CancellationToken.None);

        await store.DeleteAsync(_clip, CancellationToken.None);

        Assert.Null(await store.FindAsync(_clip, CancellationToken.None));
        Assert.False(File.Exists(store.PathOf(_clip)));
    }

    [Fact]
    public async Task EmptyLibrary_ListsNothing()
    {
        using var store = NewStore();

        Assert.Empty(await store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CorruptIndex_IsReportedAndLeftUntouched()
    {
        var index = Path.Combine(_directory.Path, FileSoundStore.IndexFileName);
        await File.WriteAllTextAsync(index, "{ not json");

        using var store = NewStore();

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ListAsync(CancellationToken.None));
        Assert.Equal("{ not json", await File.ReadAllTextAsync(index));
    }
}
