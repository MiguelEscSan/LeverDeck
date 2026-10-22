using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Actions;

public sealed class ExecuteActionTests
{
    private readonly Harness _harness = new();

    private Task<Result> Execute(Profile profile, int pageIndex, ButtonId buttonId, Caller? caller = null) =>
        _harness.ExecuteAction().ExecuteAsync(caller ?? Build.DeviceCaller, profile.Id, pageIndex, buttonId, CancellationToken.None);

    [Fact]
    public async Task ExecuteAsync_AgentAction_RunsTheStoredActionThroughTheExecutor()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.PlayPause, Build.Copy));

        var result = await Execute(profile, 0, Build.ButtonAt(profile, 0, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal([Build.Copy], _harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_AdminCaller_IsAllowed()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));

        var result = await Execute(profile, 0, Build.ButtonAt(profile, 0, 0), Caller.Admin);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutorFails_ReturnsItsError()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));
        var failure = new DomainError(DomainErrorCode.ActionNotExecutable, "OBS is not connected.");
        _harness.Executor.NextResult = Result.Failure(failure);

        var result = await Execute(profile, 0, Build.ButtonAt(profile, 0, 0));

        Assert.Equal(failure, result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProfile_FailsWithProfileNotFound()
    {
        var profile = Build.ProfileWith("Not stored", Build.Copy);

        Build.Fails(DomainErrorCode.ProfileNotFound, await Execute(profile, 0, Build.ButtonAt(profile, 0, 0)));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task ExecuteAsync_PageOutOfRange_FailsWithValidationFailed(int pageIndex)
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));

        Build.Fails(DomainErrorCode.ValidationFailed, await Execute(profile, pageIndex, Build.ButtonAt(profile, 0, 0)));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_ButtonOnAnotherPage_FailsWithButtonNotFound()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main"));
        profile.AddPage();
        var onSecondPage = profile.PlaceButton(1, new GridPosition(0, 0), Build.Appearance, Build.Copy);

        Build.Fails(DomainErrorCode.ButtonNotFound, await Execute(profile, 0, onSecondPage));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownButton_FailsWithButtonNotFound()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));

        Build.Fails(DomainErrorCode.ButtonNotFound, await Execute(profile, 0, ButtonId.New()));
    }

    [Fact]
    public async Task ExecuteAsync_ChangePage_FailsWithActionNotExecutable()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", new ChangePage(PageTarget.Next)));

        Build.Fails(DomainErrorCode.ActionNotExecutable, await Execute(profile, 0, Build.ButtonAt(profile, 0, 0)));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_SwitchProfile_FailsWithActionNotExecutable()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main"));
        var button = profile.PlaceButton(0, new GridPosition(0, 0), Build.Appearance, new SwitchProfile(profile.Id));

        Build.Fails(DomainErrorCode.ActionNotExecutable, await Execute(profile, 0, button));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_LaunchRegisteredApp_RunsIt()
    {
        var app = _harness.Settings.Stored.RegisterApp("Notepad", @"C:\Windows\notepad.exe");
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", new LaunchApp(app)));

        var result = await Execute(profile, 0, Build.ButtonAt(profile, 0, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal([new LaunchApp(app)], _harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_LaunchUnregisteredApp_FailsWithActionNotExecutable()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", new LaunchApp(AppId.New())));

        Build.Fails(DomainErrorCode.ActionNotExecutable, await Execute(profile, 0, Build.ButtonAt(profile, 0, 0)));
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_AnyOutcome_PublishesNoEventAndWritesNothing()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));

        await Execute(profile, 0, Build.ButtonAt(profile, 0, 0));
        await Execute(profile, 0, ButtonId.New());

        Assert.Empty(_harness.Events.Published);
        Assert.Equal(0, _harness.RepositoryWrites);
    }
}
