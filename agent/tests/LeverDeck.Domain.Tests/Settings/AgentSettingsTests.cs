using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Domain.Tests.Settings;

public sealed class AgentSettingsTests
{
    private const string Discord = @"C:\Apps\Discord\Discord.exe";
    private const string Spotify = @"C:\Apps\Spotify\Spotify.exe";

    private static readonly ObsConnection _obs = new("localhost", 4455, "pw");

    private static AgentSettings Restore(params RegisteredApp[] apps) =>
        AgentSettings.Restore(true, GridSize.EightByFour, _obs, apps);

    // Default / Restore

    [Fact]
    public void CreateDefault_HasDocumentedDefaults()
    {
        var settings = AgentSettings.CreateDefault();

        Assert.False(settings.StartWithWindows);
        Assert.Equal(GridSize.FiveByThree, settings.DefaultGridSize);
        Assert.Null(settings.ObsConnection);
        Assert.Empty(settings.RegisteredApps);
    }

    [Fact]
    public void CreateDefault_ReturnsIndependentInstances()
    {
        var first = AgentSettings.CreateDefault();
        first.SetStartWithWindows(true);

        Assert.False(AgentSettings.CreateDefault().StartWithWindows);
    }

    [Fact]
    public void Restore_ValidState_KeepsState()
    {
        var discord = new RegisteredApp(AppId.New(), "Discord", Discord);
        var spotify = new RegisteredApp(AppId.New(), "Spotify", Spotify);

        var settings = Restore(discord, spotify);

        Assert.True(settings.StartWithWindows);
        Assert.Equal(GridSize.EightByFour, settings.DefaultGridSize);
        Assert.Equal(_obs, settings.ObsConnection);
        Assert.Equal([discord, spotify], settings.RegisteredApps);
    }

    [Fact]
    public void Restore_DuplicateAppIds_ThrowsValidationFailed()
    {
        var id = AppId.New();

        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Restore(new RegisteredApp(id, "Discord", Discord), new RegisteredApp(id, "Spotify", Spotify)));
    }

    [Fact]
    public void Restore_DuplicatePathsDifferingOnlyInCase_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Restore(
                new RegisteredApp(AppId.New(), "Discord", Discord),
                new RegisteredApp(AppId.New(), "Discord 2", Discord.ToUpperInvariant())));
    }

    [Fact]
    public void Restore_NullArguments_ThrowArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => AgentSettings.Restore(false, null!, null, []));
        Assert.Throws<ArgumentNullException>(() => AgentSettings.Restore(false, GridSize.FiveByThree, null, null!));
        Assert.Throws<ArgumentNullException>(() => AgentSettings.Restore(false, GridSize.FiveByThree, null, [null!]));
    }

    // Simple settings

    [Fact]
    public void SetStartWithWindows_ChangesFlag()
    {
        var settings = AgentSettings.CreateDefault();

        settings.SetStartWithWindows(true);

        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void ChangeDefaultGridSize_ChangesGrid()
    {
        var settings = AgentSettings.CreateDefault();

        settings.ChangeDefaultGridSize(GridSize.ThreeByThree);

        Assert.Equal(GridSize.ThreeByThree, settings.DefaultGridSize);
    }

    [Fact]
    public void ChangeDefaultGridSize_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => AgentSettings.CreateDefault().ChangeDefaultGridSize(null!));
    }

    [Fact]
    public void ConfigureObs_Connection_IsStored()
    {
        var settings = AgentSettings.CreateDefault();

        settings.ConfigureObs(_obs);

        Assert.Equal(_obs, settings.ObsConnection);
    }

    [Fact]
    public void ConfigureObs_Null_ClearsConnection()
    {
        var settings = Restore();

        settings.ConfigureObs(null);

        Assert.Null(settings.ObsConnection);
    }

    // Registered apps

    [Fact]
    public void RegisterApp_NewPath_AddsAppAndReturnsItsId()
    {
        var settings = AgentSettings.CreateDefault();

        var id = settings.RegisterApp("Discord", Discord);

        var app = Assert.Single(settings.RegisteredApps);
        Assert.Equal(id, app.Id);
        Assert.Equal("Discord", app.DisplayName);
        Assert.Equal(Discord, app.ExecutablePath);
        Assert.Same(app, settings.FindApp(id));
    }

    [Fact]
    public void RegisterApp_DuplicatePathDifferingOnlyInCase_ThrowsValidationFailed()
    {
        var settings = AgentSettings.CreateDefault();
        settings.RegisterApp("Discord", Discord);

        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => settings.RegisterApp("Other", Discord.ToLowerInvariant()));
        Assert.Single(settings.RegisteredApps);
    }

    [Fact]
    public void RegisterApp_InvalidPath_ThrowsValidationFailedAndAddsNothing()
    {
        var settings = AgentSettings.CreateDefault();

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => settings.RegisterApp("App", "app.exe"));
        Assert.Empty(settings.RegisteredApps);
    }

    [Fact]
    public void UnregisterApp_KnownId_RemovesOnlyThatApp()
    {
        var settings = AgentSettings.CreateDefault();
        var discord = settings.RegisterApp("Discord", Discord);
        var spotify = settings.RegisterApp("Spotify", Spotify);

        settings.UnregisterApp(discord);

        Assert.Null(settings.FindApp(discord));
        Assert.Equal(spotify, Assert.Single(settings.RegisteredApps).Id);
    }

    [Fact]
    public void UnregisterApp_UnknownId_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => AgentSettings.CreateDefault().UnregisterApp(AppId.New()));
    }

    [Fact]
    public void UnregisterApp_ThenRegisterSamePath_IsAllowed()
    {
        var settings = AgentSettings.CreateDefault();
        settings.UnregisterApp(settings.RegisterApp("Discord", Discord));

        var id = settings.RegisterApp("Discord", Discord);

        Assert.Equal(id, Assert.Single(settings.RegisteredApps).Id);
    }

    [Fact]
    public void FindApp_UnknownId_ReturnsNull()
    {
        Assert.Null(AgentSettings.CreateDefault().FindApp(AppId.New()));
    }
}
