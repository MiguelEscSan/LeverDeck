using System.Buffers.Binary;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Icons;

/// <summary>
/// The image of an uploaded icon: a PNG of at most 256 KiB whose sides are 1–512 pixels. Only the signature and the
/// header are checked; the editor sends a 256 × 256 PNG it drew itself, and other formats (SVG above all, which can
/// carry scripts) are refused.
/// </summary>
public sealed class IconImage
{
    public const int MaxBytes = 256 * 1024;
    public const int MaxSide = 512;

    private static readonly byte[] _signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] _headerType = "IHDR"u8.ToArray();

    // Signature (8) + IHDR length (4) + type (4) + width (4) + height (4).
    private const int MinimumLength = 24;

    private readonly byte[] _bytes;

    public IconImage(ReadOnlySpan<byte> png)
    {
        Invariant.Require(png.Length <= MaxBytes, DomainErrorCode.ValidationFailed, $"An icon must be at most {MaxBytes / 1024} KiB.");
        Invariant.Require(
            png.Length >= MinimumLength && png[..8].SequenceEqual(_signature) && png.Slice(12, 4).SequenceEqual(_headerType),
            DomainErrorCode.ValidationFailed,
            "An icon must be a PNG image.");

        Width = BinaryPrimitives.ReadInt32BigEndian(png.Slice(16, 4));
        Height = BinaryPrimitives.ReadInt32BigEndian(png.Slice(20, 4));
        Invariant.Require(
            Width is >= 1 and <= MaxSide && Height is >= 1 and <= MaxSide,
            DomainErrorCode.ValidationFailed,
            $"An icon must be at most {MaxSide} × {MaxSide} pixels.");

        _bytes = png.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<byte> Bytes => _bytes;
}
