using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Tests.Settings;

public sealed class SettingsUseCaseTests
{
    private const string NotepadPath = @"C:\Windows\notepad.exe";

    private readonly Harness _harness = new();

    private void AssertNothingHappened()
    {
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }

    // UpdateSettings

    [Fact]
    public async Task UpdateSettings_NewValues_AppliesSavesAndPublishesSettingsUpdated()
    {
        var obs = new ObsConnection("localhost", 4455, "secret");

        var result = await _harness.UpdateSettings()
            .ExecuteAsync(Caller.Admin, true, GridSize.EightByFour, obs, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = _harness.Settings.Stored;
        Assert.True(stored.StartWithWindows);
        Assert.Equal(GridSize.EightByFour, stored.DefaultGridSize);
        Assert.Equal(obs, stored.ObsConnection);
        Assert.Equal(1, _harness.Settings.Writes);
        Assert.Equal([new SettingsUpdated()], _harness.Events.Published);
    }

    [Fact]
    public async Task UpdateSettings_NullObsConnection_ClearsIt()
    {
        _harness.Settings.Stored.ConfigureObs(new ObsConnection("localhost", 4455, null));

        await _harness.UpdateSettings().ExecuteAsync(Caller.Admin, false, GridSize.FiveByThree, null, CancellationToken.None);

        Assert.Null(_harness.Settings.Stored.ObsConnection);
    }

    [Fact]
    public async Task UpdateSettings_ExistingApps_AreKept()
    {
        var app = _harness.Settings.Stored.RegisterApp("Notepad", NotepadPath);

        await _harness.UpdateSettings().ExecuteAsync(Caller.Admin, false, GridSize.FiveByThree, null, CancellationToken.None);

        Assert.NotNull(_harness.Settings.Stored.FindApp(app));
    }

    // RegisterApp

    [Fact]
    public async Task RegisterApp_NewExecutable_ReturnsItsIdSavesAndPublishesSettingsUpdated()
    {
        var result = await _harness.RegisterApp().ExecuteAsync(Caller.Admin, "Notepad", NotepadPath, CancellationToken.None);

        var app = Assert.Single(_harness.Settings.Stored.RegisteredApps);
        Assert.Equal(app.Id, result.Value);
        Assert.Equal(NotepadPath, app.ExecutablePath);
        Assert.Equal(1, _harness.Settings.Writes);
        Assert.Equal([new SettingsUpdated()], _harness.Events.Published);
    }

    [Fact]
    public async Task RegisterApp_PathAlreadyRegisteredInAnotherCase_FailsWithValidationFailed()
    {
        _harness.Settings.Stored.RegisterApp("Notepad", NotepadPath);

        var result = await _harness.RegisterApp()
            .ExecuteAsync(Caller.Admin, "Again", NotepadPath.ToUpperInvariant(), CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        AssertNothingHappened();
    }

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("\"C:\\Windows\\notepad.exe\" /evil")]
    public async Task RegisterApp_UnsafePath_FailsWithValidationFailedWithoutThrowing(string path)
    {
        var result = await _harness.RegisterApp().ExecuteAsync(Caller.Admin, "Notepad", path, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        AssertNothingHappened();
    }

    // UnregisterApp

    [Fact]
    public async Task UnregisterApp_UnusedApp_RemovesItSavesAndPublishesSettingsUpdated()
    {
        var app = _harness.Settings.Stored.RegisterApp("Notepad", NotepadPath);
        _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));

        var result = await _harness.UnregisterApp().ExecuteAsync(Caller.Admin, app, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_harness.Settings.Stored.RegisteredApps);
        Assert.Equal(1, _harness.Settings.Writes);
        Assert.Equal([new SettingsUpdated()], _harness.Events.Published);
    }

    [Fact]
    public async Task UnregisterApp_AppUsedByAProfile_FailsWithValidationFailed()
    {
        var app = _harness.Settings.Stored.RegisterApp("Notepad", NotepadPath);
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main"));
        profile.AddPage();
        profile.PlaceButton(1, new GridPosition(0, 0), Build.Appearance, new LaunchApp(app));

        var result = await _harness.UnregisterApp().ExecuteAsync(Caller.Admin, app, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.NotNull(_harness.Settings.Stored.FindApp(app));
        AssertNothingHappened();
    }

    [Fact]
    public async Task UnregisterApp_UnknownApp_FailsWithValidationFailedWithoutThrowing()
    {
        var result = await _harness.UnregisterApp().ExecuteAsync(Caller.Admin, AppId.New(), CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        AssertNothingHappened();
    }

    // GetSettings

    [Fact]
    public async Task GetSettings_Stored_ReturnsThemWithoutSideEffects()
    {
        var result = await _harness.GetSettings().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Same(_harness.Settings.Stored, result.Value);
        AssertNothingHappened();
    }
}
