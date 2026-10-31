using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Tests.Persistence;

public sealed class JsonProfileRepositoryTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private JsonProfileRepository NewRepository() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    private string FilePath => _directory.File("profiles.json");

    [Fact]
    public async Task List_OnFirstRun_SeedsAndStoresTheDefaultProfile()
    {
        var profiles = await NewRepository().ListAsync(CancellationToken.None);

        var profile = Assert.Single(profiles);
        Assert.Equal("Default", profile.Name.Value);
        Assert.Equal(GridSize.FiveByThree, profile.GridSize);
        Assert.NotEmpty(profile.Pages[0].Buttons);
        Assert.All(profile.Pages[0].Buttons, button => Assert.IsType<MediaKey>(button.Action));
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public async Task SeededProfile_KeepsItsIdAcrossInstances()
    {
        var first = Assert.Single(await NewRepository().ListAsync(CancellationToken.None));

        var again = await NewRepository().GetByIdAsync(first.Id, CancellationToken.None);

        Assert.NotNull(again);
        Assert.Equal(first.Id, again.Id);
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsEveryActionType()
    {
        var profile = Profile.Create(new ProfileName("Everything"), KeyColor.Purple, GridSize.EightByFour);
        DomainActionSamples().Select((action, index) => (action, index)).ToList().ForEach(sample =>
            profile.PlaceButton(
                0,
                new GridPosition(sample.index / 8, sample.index % 8),
                new ButtonAppearance($"Key {sample.index}", KeyColor.Red, new IconId("star")),
                sample.action));
        profile.AddPage();

        await NewRepository().SaveAsync(profile, CancellationToken.None);
        var loaded = await NewRepository().GetByIdAsync(profile.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.Color, loaded.Color);
        Assert.Equal(profile.GridSize, loaded.GridSize);
        Assert.Equal(2, loaded.Pages.Count);
        Assert.Equal(
            profile.Pages[0].Buttons.Select(button => (button.Id, button.Position, button.Appearance, button.Action)),
            loaded.Pages[0].Buttons.Select(button => (button.Id, button.Position, button.Appearance, button.Action)));
    }

    [Fact]
    public async Task Save_ReplacesTheProfileWithTheSameId()
    {
        var repository = NewRepository();
        var seeded = Assert.Single(await repository.ListAsync(CancellationToken.None));

        seeded.Rename(new ProfileName("Renamed"));
        await repository.SaveAsync(seeded, CancellationToken.None);

        var stored = Assert.Single(await NewRepository().ListAsync(CancellationToken.None));
        Assert.Equal("Renamed", stored.Name.Value);
    }

    [Fact]
    public async Task Delete_RemovesTheProfile()
    {
        var repository = NewRepository();
        var other = Profile.Create(new ProfileName("Other"), KeyColor.Blue, GridSize.ThreeByThree);
        await repository.SaveAsync(other, CancellationToken.None);

        await repository.DeleteAsync(other.Id, CancellationToken.None);

        Assert.Null(await NewRepository().GetByIdAsync(other.Id, CancellationToken.None));
        Assert.Single(await NewRepository().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Save_LeavesNoTemporaryFileBehind()
    {
        await NewRepository().SaveAsync(
            Profile.Create(new ProfileName("Other"), KeyColor.Blue, GridSize.ThreeByThree),
            CancellationToken.None);

        Assert.Equal(["profiles.json"], Directory.GetFiles(_directory.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task CorruptFile_FailsLoudlyAndIsLeftUntouched()
    {
        await File.WriteAllTextAsync(FilePath, "{ not json");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => NewRepository().ListAsync(CancellationToken.None));

        Assert.Contains("profiles.json", error.Message, StringComparison.Ordinal);
        Assert.Equal("{ not json", await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task FileBreakingADomainRule_FailsLoudly()
    {
        await File.WriteAllTextAsync(
            FilePath,
            """{ "schemaVersion": 1, "profiles": [ { "id": "00000000-0000-0000-0000-000000000000", "name": "x", "color": "Red", "gridSize": { "columns": 5, "rows": 3 }, "pages": [ { "buttons": [] } ] } ] }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => NewRepository().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnknownSchemaVersion_FailsLoudly()
    {
        await File.WriteAllTextAsync(FilePath, """{ "schemaVersion": 99, "profiles": [] }""");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => NewRepository().ListAsync(CancellationToken.None));

        Assert.Contains("schema version 99", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentSaves_KeepEveryProfile()
    {
        var repository = NewRepository();
        var created = Enumerable.Range(0, 20)
            .Select(index => Profile.Create(new ProfileName($"P{index}"), KeyColor.Dark, GridSize.ThreeByThree))
            .ToList();

        await Task.WhenAll(created.Select(profile => Task.Run(() => repository.SaveAsync(profile, CancellationToken.None))));

        var stored = await NewRepository().ListAsync(CancellationToken.None);
        Assert.Equal(21, stored.Count);
    }

    internal static IEnumerable<LeverDeck.Domain.Actions.Action> DomainActionSamples() =>
    [
        new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, VirtualKey.S),
        new Hotkey(KeyModifiers.None, VirtualKey.F13),
        new MediaKey(MediaKeyKind.VolumeUp),
        new LaunchApp(AppId.New()),
        new OpenUrl(new Uri("https://example.com/path?q=1")),
        new ObsScene("Gaming"),
        new ObsStreaming(ObsOutputCommand.Stop),
        new ObsRecord(ObsOutputCommand.Toggle),
        new ObsInputMute("Mic", ObsMuteCommand.Toggle),
        new ObsSourceVisibility("Gaming", "Webcam", ObsVisibilityCommand.Toggle),
        new LaunchGame(GameStore.Steam, "570"),
        new LaunchGame(GameStore.Epic, "Fortnite"),
        new ChangePage(PageTarget.Next),
        new ChangePage(PageTarget.Previous),
        new ChangePage(PageTarget.At(1)),
        new SwitchProfile(ProfileId.New()),
    ];
}
