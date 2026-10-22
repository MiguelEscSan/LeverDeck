using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Devices;

public sealed class DeviceIdTests
{
    [Fact]
    public void New_IsUniqueAndNotEmpty()
    {
        var first = DeviceId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, DeviceId.New());
    }

    [Fact]
    public void Constructor_EmptyGuid_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new DeviceId(Guid.Empty));
    }

    [Fact]
    public void Equality_SameGuid_IsEqual()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new DeviceId(guid), new DeviceId(guid));
        Assert.Equal(guid.ToString(), new DeviceId(guid).ToString());
    }
}

public sealed class DeviceNameTests
{
    [Fact]
    public void Constructor_FortyChars_IsAccepted()
    {
        var value = new string('x', 40);

        Assert.Equal(value, new DeviceName(value).Value);
    }

    [Fact]
    public void Constructor_FortyOneChars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new DeviceName(new string('x', 41)));
    }

    [Fact]
    public void Constructor_SurroundingWhitespace_IsTrimmedBeforeLengthCheck()
    {
        Assert.Equal(new string('x', 40), new DeviceName($"  {new string('x', 40)}  ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationFailed(string? value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new DeviceName(value!));
    }

    [Fact]
    public void Equality_SameTrimmedValue_IsEqual()
    {
        Assert.Equal(new DeviceName("Pixel"), new DeviceName(" Pixel "));
    }
}

public sealed class TokenHashTests
{
    [Fact]
    public void Constructor_OpaqueValue_IsKeptVerbatim()
    {
        Assert.Equal(" abc= ", new TokenHash(" abc= ").Value);
    }

    [Fact]
    public void Constructor_TwoHundredFiftySixChars_IsAccepted()
    {
        var value = new string('h', 256);

        Assert.Equal(value, new TokenHash(value).Value);
    }

    [Fact]
    public void Constructor_TwoHundredFiftySevenChars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new TokenHash(new string('h', 257)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationFailed(string? value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new TokenHash(value!));
    }

    [Fact]
    public void ToString_DoesNotRevealTheHash()
    {
        Assert.DoesNotContain("secret-hash", new TokenHash("secret-hash").ToString(), StringComparison.Ordinal);
    }
}
