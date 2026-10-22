using System.Buffers.Binary;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Tests.Icons;

public sealed class IconTests
{
    /// <summary>The start of a PNG: signature and IHDR header with the given size, then <paramref name="padding"/> bytes.</summary>
    internal static byte[] Png(int width, int height, int padding = 13)
    {
        var bytes = new byte[24 + padding];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    [Theory]
    [InlineData("custom-0123456789abcdef0123456789abcdef", true)]
    [InlineData("play-pause", false)]
    [InlineData("custom-0123456789abcdef0123456789abcdeg", false)]
    [InlineData("custom-0123", false)]
    public void IsCustom_MatchesOnlyTheUploadFormat(string id, bool expected)
    {
        Assert.Equal(expected, CustomIcon.IsCustom(new IconId(id)));
    }

    [Fact]
    public void IconImage_AcceptsAPngWithinTheLimits()
    {
        var image = new IconImage(Png(256, 128));

        Assert.Equal((256, 128), (image.Width, image.Height));
        Assert.Equal(Png(256, 128), image.Bytes.ToArray());
    }

    [Fact]
    public void IconImage_RefusesOtherFormats()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        var jpeg = new byte[40];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconImage(svg));
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconImage(jpeg));
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconImage(Png(1, 1).AsSpan(0, 20)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(513, 10)]
    [InlineData(10, -1)]
    public void IconImage_RefusesSidesOutsideTheLimits(int width, int height)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconImage(Png(width, height)));
    }

    [Fact]
    public void IconImage_RefusesMoreThan256KiB()
    {
        _ = new IconImage(Png(64, 64, IconImage.MaxBytes - 24));

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconImage(Png(64, 64, IconImage.MaxBytes - 23)));
    }
}
