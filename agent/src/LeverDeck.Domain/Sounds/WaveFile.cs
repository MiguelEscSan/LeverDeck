using System.Buffers.Binary;
using System.Text;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Sounds;

/// <summary>What the agent learns from an uploaded WAV file.</summary>
public sealed record WaveInfo(int Channels, int SampleRate, int BitsPerSample, long DataBytes)
{
    /// <summary>The playing time, rounded down to whole milliseconds.</summary>
    public int DurationMs => (int)(DataBytes * 1000 / (SampleRate * Channels * BitsPerSample / 8));
}

/// <summary>
/// Checks an uploaded sound. Only uncompressed PCM WAV plays through the Windows sound API the agent uses, so every
/// other file (MP3, OGG, FLAC, or a WAVE with a compressed codec) is refused with a message that names the format.
/// The header is read here, the samples never are.
/// </summary>
public static class WaveFile
{
    public const int MaxBytes = 50 * 1024 * 1024;

    private const int RiffHeaderLength = 12;
    private const int ChunkHeaderLength = 8;
    private const int PcmFormatTag = 1;
    private const int MinimumFmtLength = 16;
    private const int MinimumRateHz = 1;
    private const int MaximumRateHz = 192_000;

    private static readonly byte[] _riff = "RIFF"u8.ToArray();
    private static readonly byte[] _wave = "WAVE"u8.ToArray();
    private static readonly byte[] _fmt = "fmt "u8.ToArray();
    private static readonly byte[] _data = "data"u8.ToArray();

    public static WaveInfo Parse(ReadOnlySpan<byte> bytes)
    {
        Invariant.Require(bytes.Length <= MaxBytes, DomainErrorCode.ValidationFailed, "A sound must be at most 50 MB.");
        Invariant.Require(IsWave(bytes), DomainErrorCode.ValidationFailed, "A sound must be an uncompressed WAV file (PCM).");

        WaveInfo? format = null;
        long dataBytes = -1;
        var offset = RiffHeaderLength;
        while (offset + ChunkHeaderLength <= bytes.Length)
        {
            var chunk = bytes[offset..];
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..8]);
            var body = chunk[ChunkHeaderLength..];
            Invariant.Require(size <= body.Length, DomainErrorCode.ValidationFailed, "The WAV file is truncated.");

            if (chunk[..4].SequenceEqual(_fmt))
            {
                format = ReadFormat(body[..(int)size]);
            }
            else if (chunk[..4].SequenceEqual(_data))
            {
                dataBytes = size;
            }

            offset += ChunkHeaderLength + (int)size + ((int)size & 1);
        }

        Invariant.Require(format is not null, DomainErrorCode.ValidationFailed, "The WAV file has no format chunk.");
        Invariant.Require(dataBytes > 0, DomainErrorCode.ValidationFailed, "The WAV file has no sound data.");
        return format! with { DataBytes = dataBytes };
    }

    private static bool IsWave(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= RiffHeaderLength && bytes[..4].SequenceEqual(_riff) && bytes.Slice(8, 4).SequenceEqual(_wave);

    private static WaveInfo ReadFormat(ReadOnlySpan<byte> fmt)
    {
        Invariant.Require(fmt.Length >= MinimumFmtLength, DomainErrorCode.ValidationFailed, "The WAV format chunk is too short.");
        var tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[0..2]);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..4]);
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..8]);
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(fmt[12..14]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..16]);

        Invariant.Require(tag == PcmFormatTag, DomainErrorCode.ValidationFailed, "A sound must be an uncompressed WAV file (PCM).");
        Invariant.Require(channels is 1 or 2, DomainErrorCode.ValidationFailed, "A sound must be mono or stereo.");
        Invariant.Require(bits is 8 or 16 or 24 or 32, DomainErrorCode.ValidationFailed, "A sound must have 8, 16, 24 or 32 bits per sample.");
        Invariant.Require(sampleRate is >= MinimumRateHz and <= MaximumRateHz, DomainErrorCode.ValidationFailed, "A sound's sample rate is out of range.");
        Invariant.Require(blockAlign == channels * bits / 8, DomainErrorCode.ValidationFailed, "The WAV format chunk is inconsistent.");

        return new WaveInfo(channels, (int)sampleRate, bits, DataBytes: 0);
    }
}
