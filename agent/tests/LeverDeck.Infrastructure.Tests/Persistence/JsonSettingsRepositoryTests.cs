using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Tests.Persistence;

public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private JsonSettingsRepository NewRepository() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    private string FilePath => _directory.File("settings.json");

    [Fact]
    public async Task Get_OnFirstRun_ReturnsTheDefaultsAndWritesNothing()
    {
        var settings = await NewRepository().GetAsync(CancellationToken.None);

        var defaults = AgentSettings.CreateDefault();
        Assert.Equal(defaults.StartWithWindows, settings.StartWithWindows);
        Assert.Equal(defaults.DefaultGridSize, settings.DefaultGridSize);
        Assert.Null(settings.ObsConnection);
        Assert.Empty(settings.RegisteredApps);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsEveryField()
    {
        var settings = AgentSettings.CreateDefault();
        settings.SetStartWithWindows(true);
        settings.ChangeDefaultGridSize(GridSize.EightByFour);
        settings.ConfigureObs(new ObsConnection("localhost", 4455, "s3cret-pass"));
        var appId = settings.RegisterApp("Notepad", @"C:\Windows\notepad.exe");

        await NewRepository().SaveAsync(settings, CancellationToken.None);
        var loaded = await NewRepository().GetAsync(CancellationToken.None);

        Assert.True(loaded.StartWithWindows);
        Assert.Equal(GridSize.EightByFour, loaded.DefaultGridSize);
        Assert.Equal(new ObsConnection("localhost", 4455, "s3cret-pass"), loaded.ObsConnection);
        var app = Assert.Single(loaded.RegisteredApps);
        Assert.Equal((appId, "Notepad", @"C:\Windows\notepad.exe"), (app.Id, app.DisplayName, app.ExecutablePath));
    }

    [Fact]
    public async Task Save_NeverWritesTheObsPasswordInPlainText()
    {
        var settings = AgentSettings.CreateDefault();
        settings.ConfigureObs(new ObsConnection("localhost", 4455, "s3cret-pass"));

        await NewRepository().SaveAsync(settings, CancellationToken.None);

        Assert.DoesNotContain("s3cret-pass", await File.ReadAllTextAsync(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_WithoutPassword_RoundTripsNoPassword()
    {
        var settings = AgentSettings.CreateDefault();
        settings.ConfigureObs(new ObsConnection("obs.lan", 4455, null));

        await NewRepository().SaveAsync(settings, CancellationToken.None);
        var loaded = await NewRepository().GetAsync(CancellationToken.None);

        Assert.False(loaded.ObsConnection?.HasPassword);
    }

    [Fact]
    public async Task CorruptFile_FailsLoudlyAndIsLeftUntouched()
    {
        await File.WriteAllTextAsync(FilePath, "[]");

        await Assert.ThrowsAsync<InvalidDataException>(() => NewRepository().GetAsync(CancellationToken.None));
        Assert.Equal("[]", await File.ReadAllTextAsync(FilePath));
    }
}
