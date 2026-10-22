using System.Buffers.Binary;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Icons;

namespace LeverDeck.Infrastructure.Tests.Icons;

public sealed class FileIconStoreTests : IDisposable
{
    private static readonly IconId _icon = new("custom-0123456789abcdef0123456789abcdef");

    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private FileIconStore NewStore() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    internal static byte[] Png(int side = 64)
    {
        var bytes = new byte[40];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), side);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), side);
        return bytes;
    }

    [Fact]
    public async Task Save_ThenList_ExistsAndReadBack()
    {
        var store = NewStore();

        await store.SaveAsync(_icon, new IconImage(Png()), CancellationToken.None);

        Assert.Equal([_icon], await NewStore().ListAsync(CancellationToken.None));
        Assert.True(await NewStore().ExistsAsync(_icon, CancellationToken.None));
        Assert.Equal(Png(), await File.ReadAllBytesAsync(Path.Combine(_directory.Path, "icons", _icon.Value + ".png")));
    }

    [Fact]
    public async Task List_IgnoresFilesThatAreNotUploadedIcons()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_directory.Path, "icons")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(folder, "notes.png"), Png());
        await File.WriteAllBytesAsync(Path.Combine(folder, "custom-0123.png"), Png());
        await File.WriteAllBytesAsync(Path.Combine(folder, _icon.Value + ".txt"), Png());

        Assert.Empty(await NewStore().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task List_WithoutAFolder_IsEmpty()
    {
        Assert.Empty(await NewStore().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesTheFile()
    {
        var store = NewStore();
        await store.SaveAsync(_icon, new IconImage(Png()), CancellationToken.None);

        await store.DeleteAsync(_icon, CancellationToken.None);

        Assert.False(await store.ExistsAsync(_icon, CancellationToken.None));
    }

    [Fact]
    public async Task PathOf_NeverNamesAFileForABundledIcon()
    {
        Assert.Null(NewStore().PathOf(new IconId("play-pause")));
        await Assert.ThrowsAsync<ArgumentException>(() => NewStore().SaveAsync(new IconId("play-pause"), new IconImage(Png()), CancellationToken.None));
    }

    [Theory]
    [InlineData("custom-0123456789abcdef0123456789abcdef", true)]
    [InlineData("../settings", false)]
    [InlineData("custom-0123456789abcdef0123456789abcdef/../x", false)]
    [InlineData(null, false)]
    public void TryParse_AcceptsOnlyUploadedIconIds(string? name, bool expected)
    {
        Assert.Equal(expected, FileIconStore.TryParse(name) is not null);
    }
}
