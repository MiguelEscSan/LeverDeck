namespace LeverDeck.Application.Tests.Status;

public sealed class GetAgentStatusTests
{
    [Fact]
    public async Task ExecuteAsync_DeviceCaller_ReturnsProtocolAndAgentVersions()
    {
        var result = await Harness.GetAgentStatus().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.Equal(ApplicationInfo.ProtocolVersion, result.Value.ProtocolVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AgentVersion));
        Assert.Equal(ApplicationInfo.AgentVersion, result.Value.AgentVersion);
    }
}
