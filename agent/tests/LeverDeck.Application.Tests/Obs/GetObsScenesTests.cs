using LeverDeck.Application.Obs;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Tests.Obs;

public sealed class GetObsScenesTests
{
    [Fact]
    public async Task ExecuteAsync_Connected_ReturnsTheScenesAndTheCurrentOne()
    {
        var harness = new Harness();

        var result = await harness.GetObsScenes().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Gameplay", "Starting soon"], result.Value.Scenes);
        Assert.Equal("Gameplay", result.Value.CurrentScene);
    }

    [Fact]
    public async Task ExecuteAsync_NotConnected_FailsWithActionFailed()
    {
        var harness = new Harness();
        harness.Obs.IsConnected = false;

        var result = await harness.GetObsScenes().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_PublicCaller_IsUnauthorized()
    {
        var harness = new Harness();

        var result = await harness.GetObsScenes().ExecuteAsync(Caller.Public, CancellationToken.None);

        Assert.Equal(DomainErrorCode.Unauthorized, result.Error?.Code);
        Assert.Empty(harness.Obs.Commands);
    }
}
