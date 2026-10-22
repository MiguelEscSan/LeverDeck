using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Profiles;

public sealed class SaveProfileTests
{
    private readonly Harness _harness = new();

    private Task<Result> Save(Profile profile) =>
        _harness.SaveProfile().ExecuteAsync(Caller.Admin, profile, CancellationToken.None);

    private void AssertNothingHappened()
    {
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_NewProfile_SavesItAndPublishesProfileUpdated()
    {
        var profile = Build.ProfileWith("Main", Build.Copy);

        var result = await Save(profile);

        Assert.True(result.IsSuccess);
        Assert.Same(profile, Assert.Single(_harness.Profiles.All));
        Assert.Equal([new ProfileUpdated(profile.Id)], _harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingProfileKeepingItsNameInAnotherCase_ReplacesIt()
    {
        var stored = _harness.Profiles.Add(Build.ProfileWith("Main"));
        var edited = Profile.Restore(stored.Id, new ProfileName("MAIN"), KeyColor.Red, GridSize.ThreeByThree, [new Page(0, [])]);

        var result = await Save(edited);

        Assert.True(result.IsSuccess);
        Assert.Same(edited, Assert.Single(_harness.Profiles.All));
    }

    [Fact]
    public async Task ExecuteAsync_NameUsedByAnotherProfileIgnoringCase_FailsWithDuplicateProfileName()
    {
        _harness.Profiles.Add(Build.ProfileWith("Gaming"));

        Build.Fails(DomainErrorCode.DuplicateProfileName, await Save(Build.ProfileWith("gAMING")));
        AssertNothingHappened();
    }

    [Fact]
    public async Task ExecuteAsync_LaunchRegisteredApp_IsAccepted()
    {
        var app = _harness.Settings.Stored.RegisterApp("Notepad", @"C:\Windows\notepad.exe");

        Assert.True((await Save(Build.ProfileWith("Main", new LaunchApp(app)))).IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_LaunchUnregisteredApp_FailsWithValidationFailed()
    {
        Build.Fails(DomainErrorCode.ValidationFailed, await Save(Build.ProfileWith("Main", new LaunchApp(AppId.New()))));
        AssertNothingHappened();
    }

    [Fact]
    public async Task ExecuteAsync_SwitchToItself_IsAccepted()
    {
        var profile = Build.ProfileWith("Main");
        profile.PlaceButton(0, new GridPosition(0, 0), Build.Appearance, new SwitchProfile(profile.Id));

        Assert.True((await Save(profile)).IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_SwitchToAnotherStoredProfile_IsAccepted()
    {
        var other = _harness.Profiles.Add(Build.ProfileWith("Other"));

        Assert.True((await Save(Build.ProfileWith("Main", new SwitchProfile(other.Id)))).IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_SwitchToMissingProfile_FailsWithValidationFailed()
    {
        Build.Fails(DomainErrorCode.ValidationFailed, await Save(Build.ProfileWith("Main", new SwitchProfile(ProfileId.New()))));
        AssertNothingHappened();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidActionOnSecondPage_IsStillRejected()
    {
        var profile = Build.ProfileWith("Main");
        profile.AddPage();
        profile.PlaceButton(1, new GridPosition(0, 0), Build.Appearance, new LaunchApp(AppId.New()));

        Build.Fails(DomainErrorCode.ValidationFailed, await Save(profile));
    }
}
