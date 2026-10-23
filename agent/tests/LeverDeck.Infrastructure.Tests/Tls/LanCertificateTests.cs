using System.Net;
using LeverDeck.Infrastructure.Discovery;
using LeverDeck.Infrastructure.Tests.Discovery;
using LeverDeck.Infrastructure.Tls;

namespace LeverDeck.Infrastructure.Tests.Tls;

public sealed class LanCertificateTests
{
    private static readonly AgentHostName Studio = AgentHostNames.Of("leverdeck-studio");

    [Fact]
    public void NamesCoverThisPcsMdnsNameLoopbackAndTheLanAddress()
    {
        var names = LanCertificate.NamesFor(Studio, IPAddress.Parse("192.168.1.38"));

        Assert.Equal(["leverdeck-studio.local", "localhost", "127.0.0.1", "192.168.1.38"], names);
    }

    [Fact]
    public void NamesWithoutALanAddressStillCoverTheMdnsName()
    {
        Assert.Equal(["leverdeck-studio.local", "localhost", "127.0.0.1"], LanCertificate.NamesFor(Studio, null));
    }

    [Fact]
    public void IssuesWhenThereIsNoCertificateYet()
    {
        Assert.True(LanCertificate.NeedsIssue(hasCertificate: false, storedNames: null, desired: ["leverdeck-studio.local"]));
    }

    [Fact]
    public void IssuesAgainWhenTheLanAddressChanged()
    {
        var stored = "leverdeck-studio.local localhost 127.0.0.1 192.168.1.38";

        Assert.True(LanCertificate.NeedsIssue(
            hasCertificate: true,
            storedNames: stored,
            desired: ["leverdeck-studio.local", "localhost", "127.0.0.1", "192.168.1.50"]));
    }

    [Fact]
    public void IssuesAgainWhenTheHostNameChanged()
    {
        var stored = "leverdeck.local localhost 127.0.0.1 192.168.1.38";

        Assert.True(LanCertificate.NeedsIssue(
            hasCertificate: true,
            storedNames: stored,
            desired: LanCertificate.NamesFor(Studio, IPAddress.Parse("192.168.1.38"))));
    }

    [Fact]
    public void KeepsACertificateThatCoversTheCurrentNames()
    {
        var names = LanCertificate.NamesFor(Studio, IPAddress.Parse("192.168.1.38"));

        Assert.False(LanCertificate.NeedsIssue(hasCertificate: true, storedNames: string.Join(' ', names), desired: names));
    }
}
