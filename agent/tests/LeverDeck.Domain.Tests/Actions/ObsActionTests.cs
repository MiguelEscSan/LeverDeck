using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Actions;

public sealed class ObsStreamingTests
{
    [Fact]
    public void Constructor_Command_KeepsItAndRunsOnAgent()
    {
        var streaming = new ObsStreaming(ObsOutputCommand.Start);

        Assert.Equal(ObsOutputCommand.Start, streaming.Command);
        Assert.Equal(ExecutionTarget.Agent, streaming.Target);
    }

    [Fact]
    public void Constructor_UndefinedCommand_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsStreaming((ObsOutputCommand)99));
    }
}

public sealed class ObsRecordTests
{
    [Fact]
    public void Constructor_Command_KeepsItAndRunsOnAgent()
    {
        var record = new ObsRecord(ObsOutputCommand.Toggle);

        Assert.Equal(ObsOutputCommand.Toggle, record.Command);
        Assert.Equal(ExecutionTarget.Agent, record.Target);
    }

    [Fact]
    public void Constructor_UndefinedCommand_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsRecord((ObsOutputCommand)99));
    }
}

public sealed class ObsInputMuteTests
{
    [Fact]
    public void Constructor_NameAndCommand_TrimsNameAndRunsOnAgent()
    {
        var mute = new ObsInputMute("  Mic  ", ObsMuteCommand.Unmute);

        Assert.Equal("Mic", mute.InputName);
        Assert.Equal(ObsMuteCommand.Unmute, mute.Command);
        Assert.Equal(ExecutionTarget.Agent, mute.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankName_ThrowsValidationFailed(string? name)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsInputMute(name!, ObsMuteCommand.Mute));
    }

    [Fact]
    public void Constructor_NameLongerThanOneHundred_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ObsInputMute(new string('i', ObsScene.MaxLength + 1), ObsMuteCommand.Mute));
    }

    [Fact]
    public void Constructor_UndefinedCommand_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsInputMute("Mic", (ObsMuteCommand)99));
    }
}

public sealed class ObsSourceVisibilityTests
{
    [Fact]
    public void Constructor_SceneSourceAndCommand_TrimsNamesAndRunsOnAgent()
    {
        var visibility = new ObsSourceVisibility(" Gameplay ", " Webcam ", ObsVisibilityCommand.Hide);

        Assert.Equal("Gameplay", visibility.SceneName);
        Assert.Equal("Webcam", visibility.SourceName);
        Assert.Equal(ObsVisibilityCommand.Hide, visibility.Command);
        Assert.Equal(ExecutionTarget.Agent, visibility.Target);
    }

    [Theory]
    [InlineData("", "Webcam")]
    [InlineData("Gameplay", "")]
    [InlineData(" ", "Webcam")]
    public void Constructor_BlankSceneOrSource_ThrowsValidationFailed(string scene, string source)
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ObsSourceVisibility(scene, source, ObsVisibilityCommand.Show));
    }

    [Fact]
    public void Constructor_UndefinedCommand_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ObsSourceVisibility("Gameplay", "Webcam", (ObsVisibilityCommand)99));
    }
}
