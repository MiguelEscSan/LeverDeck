using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Desktop;

namespace LeverDeck.Infrastructure.Tests.Desktop;

public sealed class WindowPlacementStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private WindowPlacementStore NewStore() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    [Fact]
    public void Load_WithoutAFile_IsNull()
    {
        Assert.Null(NewStore().Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsThePlacementInAnotherInstance()
    {
        var placement = new WindowPlacement(-1200, 40, 1100, 760, Maximized: true);

        NewStore().Save(placement);

        Assert.Equal(placement, NewStore().Load());
        Assert.True(File.Exists(_directory.File(WindowPlacementStore.FileName)));
        Assert.False(File.Exists(_directory.File(WindowPlacementStore.FileName + ".tmp")));
    }

    [Fact]
    public void Load_RaisesASizeBelowTheMinimum()
    {
        NewStore().Save(new WindowPlacement(10, 20, 30, 40, Maximized: false));

        Assert.Equal(
            new WindowPlacement(10, 20, WindowPlacement.MinimumWidth, WindowPlacement.MinimumHeight, Maximized: false),
            NewStore().Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":2,"x":0,"y":0,"width":800,"height":600,"maximized":false}""")]
    [InlineData("""{"schemaVersion":1,"x":0,"y":0}""")]
    [InlineData("""{"schemaVersion":1,"x":0,"y":0,"width":900000,"height":600,"maximized":false}""")]
    public void Load_OfAFileItCannotUse_IsNullSoTheWindowOpensAtItsDefaultPlace(string content)
    {
        File.WriteAllText(_directory.File(WindowPlacementStore.FileName), content);

        Assert.Null(NewStore().Load());
    }

    [Fact]
    public void Save_CreatesTheDataDirectoryWhenMissing()
    {
        var store = new WindowPlacementStore(new LeverDeckOptions { DataDirectory = Path.Combine(_directory.Path, "fresh") });

        store.Save(new WindowPlacement(0, 0, 800, 600, Maximized: false));

        Assert.True(File.Exists(Path.Combine(_directory.Path, "fresh", WindowPlacementStore.FileName)));
    }
}
