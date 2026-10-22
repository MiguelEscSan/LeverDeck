using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Domain.Tests.Pairing;

public sealed class PairingCodeTests
{
    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("999999")]
    public void Constructor_SixAsciiDigits_KeepsValue(string value)
    {
        var code = new PairingCode(value);

        Assert.Equal(value, code.Value);
        Assert.Equal(value, code.ToString());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData(" 123456")]
    [InlineData("123456 ")]
    [InlineData("123 456")]
    [InlineData("12345a")]
    [InlineData("-12345")]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_NotSixAsciiDigits_ThrowsValidationFailed(string? value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new PairingCode(value!));
    }

    [Theory]
    [InlineData("１２３４５６")] // full-width digits
    [InlineData("١٢٣٤٥٦")] // Arabic-Indic digits
    [InlineData("12345٦")] // one non-ASCII digit
    public void Constructor_UnicodeDigits_ThrowsValidationFailed(string value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new PairingCode(value));
    }

    [Fact]
    public void Equality_SameDigits_IsEqual()
    {
        Assert.Equal(new PairingCode("042137"), new PairingCode("042137"));
    }
}
