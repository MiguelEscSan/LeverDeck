namespace LeverDeck.Application.Tests.Status;

public sealed class GetAgentStatusTests
{
    [Fact]
    public async Task ExecuteAsync_DeviceCaller_ReturnsProtocolAndAgentVersions()
    {
        var result = await new Harness().GetAgentStatus().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.Equal(ApplicationInfo.ProtocolVersion, result.Value.ProtocolVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AgentVersion));
        Assert.Equal(ApplicationInfo.AgentVersion, result.Value.AgentVersion);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsWhetherObsIsConnected()
    {
        var harness = new Harness();
        harness.Obs.IsConnected = false;

        var result = await harness.GetAgentStatus().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.False(result.Value.ObsConnected);
    }
}
