using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Domain.Tests.Sounds;

public sealed class SoundIdTests
{
    [Fact]
    public void Constructor_OwnFormat_IsAccepted()
    {
        var id = new SoundId("sound-0123456789abcdef0123456789abcdef");

        Assert.Equal("sound-0123456789abcdef0123456789abcdef", id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("custom-0123456789abcdef0123456789abcdef")]
    [InlineData("sound-0123456789ABCDEF0123456789abcdef")]
    [InlineData("sound-../../settings")]
    [InlineData("sound-0123456789abcdef0123456789abcdef.wav")]
    public void Constructor_AnythingElse_ThrowsValidationFailed(string value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new SoundId(value));
    }

    [Fact]
    public void TryParse_OnlyTheOwnFormat_IsKnown()
    {
        Assert.NotNull(SoundId.TryParse("sound-0123456789abcdef0123456789abcdef"));
        Assert.Null(SoundId.TryParse("sound-nope"));
        Assert.Null(SoundId.TryParse(null));
    }
}

public sealed class SoundNameTests
{
    [Fact]
    public void Constructor_TrimsTheName()
    {
        Assert.Equal("Applause", new SoundName("  Applause  ").Value);
    }

    [Fact]
    public void Constructor_SixtyCharacters_IsAccepted()
    {
        var name = new string('a', SoundName.MaxLength);

        Assert.Equal(name, new SoundName(name).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tab\there")]
    [InlineData("new\nline")]
    public void Constructor_EmptyOrControlCharacters_ThrowsValidationFailed(string value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new SoundName(value));
    }

    [Fact]
    public void Constructor_TooLong_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new SoundName(new string('a', SoundName.MaxLength + 1)));
    }
}

public sealed class WaveFileTests
{
    [Fact]
    public void Parse_Stereo16BitOneSecond_ReportsTheDuration()
    {
        var info = WaveFile.Parse(WaveTestData.Pcm(channels: 2, sampleRate: 44100, bitsPerSample: 16, dataBytes: 44100 * 4));

        Assert.Equal(2, info.Channels);
        Assert.Equal(44100, info.SampleRate);
        Assert.Equal(16, info.BitsPerSample);
        Assert.Equal(1000, info.DurationMs);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void Parse_CommonPcmDepths_AreAccepted(int bits)
    {
        Assert.Equal(bits, WaveFile.Parse(WaveTestData.Pcm(channels: 1, bitsPerSample: bits, dataBytes: 1000 * bits / 8)).BitsPerSample);
    }

    [Fact]
    public void Parse_NotARiffFile_ThrowsValidationFailed()
    {
        var bytes = "This is not audio, just text.............."u8.ToArray();

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(bytes));
    }

    [Fact]
    public void Parse_Mp3Container_ThrowsValidationFailedWithTheFormatInTheMessage()
    {
        var mp3 = new byte[64];
        "ID3"u8.CopyTo(mp3);

        var error = Assert.ThrowsAny<DomainException>(() => WaveFile.Parse(mp3));

        Assert.Equal(DomainErrorCode.ValidationFailed, error.Code);
        Assert.Contains("WAV", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CompressedWave_ThrowsValidationFailed()
    {
        // format tag 0x0055 is MP3 inside a WAVE container; only uncompressed PCM plays.
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(WaveTestData.Pcm(formatTag: 0x55)));
    }

    [Fact]
    public void Parse_MoreThanTwoChannels_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(WaveTestData.Pcm(channels: 6)));
    }

    [Fact]
    public void Parse_NoDataChunk_ThrowsValidationFailed()
    {
        var bytes = WaveTestData.Pcm(dataBytes: 0);

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(bytes));
    }

    [Fact]
    public void Parse_LargerThanTheLimit_ThrowsValidationFailed()
    {
        var bytes = new byte[WaveFile.MaxBytes + 1];

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(bytes));
    }

    [Fact]
    public void Parse_Truncated_ThrowsValidationFailed()
    {
        var bytes = WaveTestData.Pcm()[..30];

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = WaveFile.Parse(bytes));
    }
}

public sealed class SoundClipTests
{
    [Fact]
    public void Clip_KeepsItsValues()
    {
        var clip = new SoundClip(new SoundId("sound-0123456789abcdef0123456789abcdef"), new SoundName("Applause"), 3000, 4096);

        Assert.Equal("Applause", clip.Name.Value);
        Assert.Equal(3000, clip.DurationMs);
        Assert.Equal(4096, clip.SizeBytes);
    }

    [Fact]
    public void Rename_KeepsTheIdAndChangesOnlyTheName()
    {
        var clip = new SoundClip(new SoundId("sound-0123456789abcdef0123456789abcdef"), new SoundName("Applause"), 3000, 4096);

        var renamed = clip.Renamed(new SoundName("Crowd"));

        Assert.Equal(clip.Id, renamed.Id);
        Assert.Equal("Crowd", renamed.Name.Value);
        Assert.Equal(3000, renamed.DurationMs);
    }
}
