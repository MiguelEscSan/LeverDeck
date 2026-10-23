using LeverDeck.App.Desktop;

namespace LeverDeck.App.Tests.Desktop;

public sealed class LaunchTests
{
    [Fact]
    public void FromArguments_WithoutTheBackgroundSwitch_IsAManualLaunch()
    {
        Assert.Equal(LaunchKind.Manual, Launch.FromArguments([]));
        Assert.Equal(LaunchKind.Manual, Launch.FromArguments(["--LeverDeck:Mdns:Enabled=false"]));
    }

    [Theory]
    [InlineData("--background")]
    [InlineData("--BACKGROUND")]
    public void FromArguments_WithTheBackgroundSwitch_IsTheSignInStart(string argument)
    {
        Assert.Equal(LaunchKind.SignIn, Launch.FromArguments(["--LeverDeck:Mdns:Enabled=false", argument]));
    }

    [Fact]
    public void HostArguments_DropOnlyTheBackgroundSwitch()
    {
        // The host's command-line configuration would read "--background" as a key and take the next argument as its value.
        Assert.Equal(
            ["--LeverDeck:Mdns:Enabled=false", "--urls", "x"],
            Launch.HostArguments(["--background", "--LeverDeck:Mdns:Enabled=false", "--urls", "x"]));
    }

    [Theory]
    [InlineData(true, LaunchKind.Manual, InstanceAction.RunAndShowWindow)]
    [InlineData(true, LaunchKind.SignIn, InstanceAction.RunInBackground)]
    [InlineData(false, LaunchKind.Manual, InstanceAction.ShowRunningInstanceAndExit)]
    [InlineData(false, LaunchKind.SignIn, InstanceAction.ExitQuietly)]
    public void Decide_RunsOneInstanceAndShowsTheWindowOnlyForAManualLaunch(bool isFirstInstance, LaunchKind launch, InstanceAction expected)
    {
        Assert.Equal(expected, Launch.Decide(isFirstInstance, launch));
    }

    [Theory]
    [InlineData(InstanceAction.RunAndShowWindow, true)]
    [InlineData(InstanceAction.RunInBackground, true)]
    [InlineData(InstanceAction.ShowRunningInstanceAndExit, false)]
    [InlineData(InstanceAction.ExitQuietly, false)]
    public void RunsTheAgent_OnlyForTheFirstInstance(InstanceAction action, bool expected)
    {
        Assert.Equal(expected, action.RunsTheAgent());
    }
}
