using LeverDeck.Application;
using LeverDeck.Protocol;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class ProtocolVersionTests
{
    // Protocol references no other project, so the version is declared twice; this keeps both in step.
    [Fact]
    public void ApplicationAndProtocol_AgreeOnTheVersion()
    {
        Assert.Equal(ProtocolInfo.ProtocolVersion, ApplicationInfo.ProtocolVersion);
    }
}
