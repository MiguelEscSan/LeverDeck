using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Domain.Tests.Settings;

public sealed class ObsConnectionTests
{
    [Fact]
    public void Constructor_ValidValues_KeepsState()
    {
        var obs = new ObsConnection(" localhost ", 4455, "s3cret");

        Assert.Equal("localhost", obs.Host);
        Assert.Equal(4455, obs.Port);
        Assert.Equal("s3cret", obs.Password);
        Assert.True(obs.HasPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_NoPassword_HasNoPassword(string? password)
    {
        var obs = new ObsConnection("localhost", 4455, password);

        Assert.Null(obs.Password);
        Assert.False(obs.HasPassword);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyHost_ThrowsValidationFailed(string? host)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsConnection(host!, 4455, null));
    }

    [Fact]
    public void Constructor_Host253Chars_IsAccepted()
    {
        var host = new string('h', 253);

        Assert.Equal(host, new ObsConnection(host, 4455, null).Host);
    }

    [Fact]
    public void Constructor_Host254Chars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ObsConnection(new string('h', 254), 4455, null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void Constructor_PortAtBounds_IsAccepted(int port)
    {
        Assert.Equal(port, new ObsConnection("localhost", port, null).Port);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void Constructor_PortOutOfRange_ThrowsValidationFailed(int port)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsConnection("localhost", port, null));
    }

    [Fact]
    public void Constructor_Password256Chars_IsAccepted()
    {
        var password = new string('p', 256);

        Assert.Equal(password, new ObsConnection("localhost", 4455, password).Password);
    }

    [Fact]
    public void Constructor_Password257Chars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ObsConnection("localhost", 4455, new string('p', 257)));
    }

    [Fact]
    public void ToString_DoesNotRevealThePassword()
    {
        Assert.DoesNotContain("s3cret", new ObsConnection("localhost", 4455, "s3cret").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_SameValues_IsEqual()
    {
        Assert.Equal(new ObsConnection("localhost", 4455, "pw"), new ObsConnection("localhost", 4455, "pw"));
    }
}
