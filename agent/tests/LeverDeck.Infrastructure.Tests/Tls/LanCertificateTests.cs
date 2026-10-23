using System.Net;
using LeverDeck.Infrastructure.Tls;

namespace LeverDeck.Infrastructure.Tests.Tls;

public sealed class LanCertificateTests
{
    [Fact]
    public void NamesCoverTheMdnsNameLoopbackAndTheLanAddress()
    {
        var names = LanCertificate.NamesFor(IPAddress.Parse("192.168.1.38"));

        Assert.Equal(["leverdeck.local", "localhost", "127.0.0.1", "192.168.1.38"], names);
    }

    [Fact]
    public void NamesWithoutALanAddressStillCoverTheMdnsName()
    {
        Assert.Equal(["leverdeck.local", "localhost", "127.0.0.1"], LanCertificate.NamesFor(null));
    }

    [Fact]
    public void IssuesWhenThereIsNoCertificateYet()
    {
        Assert.True(LanCertificate.NeedsIssue(hasCertificate: false, storedNames: null, desired: ["leverdeck.local"]));
    }

    [Fact]
    public void IssuesAgainWhenTheLanAddressChanged()
    {
        var stored = "leverdeck.local localhost 127.0.0.1 192.168.1.38";

        Assert.True(LanCertificate.NeedsIssue(
            hasCertificate: true,
            storedNames: stored,
            desired: ["leverdeck.local", "localhost", "127.0.0.1", "192.168.1.50"]));
    }

    [Fact]
    public void KeepsACertificateThatCoversTheCurrentNames()
    {
        var names = LanCertificate.NamesFor(IPAddress.Parse("192.168.1.38"));

        Assert.False(LanCertificate.NeedsIssue(hasCertificate: true, storedNames: string.Join(' ', names), desired: names));
    }
}
