using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;

namespace LeverDeck.Infrastructure.Tests.Discovery;

public sealed class AgentHostNameStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private AgentHostNameStore NewStore(string machineName = "Office PC", string? overrideName = null) =>
        new(new LeverDeckOptions { DataDirectory = _directory.Path, MdnsHostName = overrideName }, () => machineName, () => "k3x9qa");

    [Fact]
    public void Resolve_NamesThePcOnceAndStoresTheName()
    {
        var name = NewStore().Resolve();

        Assert.Equal("leverdeck-office-pc", name.Label);
        Assert.Equal("leverdeck-office-pc", File.ReadAllText(_directory.File("hostname")).Trim());
    }

    [Fact]
    public void Resolve_WritesTheNameThroughATemporaryFileThatAnInterruptedRunLeftHalfWritten()
    {
        File.WriteAllText(_directory.File("hostname.tmp"), "leverdeck-off");

        var name = NewStore().Resolve();

        Assert.Equal("leverdeck-office-pc", name.Label);
        Assert.Equal("leverdeck-office-pc", File.ReadAllText(_directory.File("hostname")).Trim());
        Assert.False(File.Exists(_directory.File("hostname.tmp")));
    }

    [Fact]
    public void Resolve_KeepsTheStoredNameWhenThePcIsRenamed()
    {
        NewStore(machineName: "Office PC").Resolve();

        Assert.Equal("leverdeck-office-pc", NewStore(machineName: "Studio").Resolve().Label);
    }

    [Fact]
    public void Resolve_CreatesTheDataDirectoryWhenMissing()
    {
        var store = new AgentHostNameStore(
            new LeverDeckOptions { DataDirectory = Path.Combine(_directory.Path, "fresh") },
            () => "Office PC",
            () => "k3x9qa");

        store.Resolve();

        Assert.True(File.Exists(Path.Combine(_directory.Path, "fresh", "hostname")));
    }

    [Fact]
    public void Resolve_ReplacesAStoredNameThatIsNotValid()
    {
        File.WriteAllText(_directory.File("hostname"), "not a label!");

        Assert.Equal("leverdeck-office-pc", NewStore().Resolve().Label);
        Assert.Equal("leverdeck-office-pc", File.ReadAllText(_directory.File("hostname")).Trim());
    }

    [Fact]
    public void Resolve_PrefersTheConfiguredNameAndDoesNotStoreIt()
    {
        var name = NewStore(overrideName: "Studio.local").Resolve();

        Assert.Equal("studio", name.Label);
        Assert.False(File.Exists(_directory.File("hostname")));
    }

    [Fact]
    public void Resolve_RefusesAConfiguredNameThatIsNotOneLabel()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NewStore(overrideName: "my studio").Resolve());

        Assert.Contains(LeverDeckOptions.MdnsHostNameKey, error.Message, StringComparison.Ordinal);
    }
}
