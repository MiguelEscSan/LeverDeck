using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Infrastructure.Tests.Icons;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class IconMapperTests
{
    [Fact]
    public void ToImage_DecodesABase64Png()
    {
        var result = IconMapper.ToImage(Convert.ToBase64String(FileIconStoreTests.Png(128)));

        Assert.True(result.IsSuccess);
        Assert.Equal(128, result.Value.Width);
        Assert.Equal(FileIconStoreTests.Png(128), result.Value.Bytes.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not base64!")]
    [InlineData("PHN2Zz48L3N2Zz4=")]
    public void ToImage_RefusesAnythingButABase64Png(string? data)
    {
        Assert.Equal(DomainErrorCode.ValidationFailed, IconMapper.ToImage(data).Error?.Code);
    }

    [Fact]
    public void ToIconId_RefusesMalformedIds()
    {
        Assert.Equal(DomainErrorCode.ValidationFailed, IconMapper.ToIconId("Not An Id").Error?.Code);
        Assert.True(IconMapper.ToIconId("custom-0123456789abcdef0123456789abcdef").IsSuccess);
    }
}
