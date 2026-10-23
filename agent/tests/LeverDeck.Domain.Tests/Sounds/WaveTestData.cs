using System.Buffers.Binary;
using System.Text;

namespace LeverDeck.Domain.Tests.Sounds;

/// <summary>Builds minimal RIFF/WAVE files for the tests: a PCM <c>fmt </c> chunk and a <c>data</c> chunk.</summary>
internal static class WaveTestData
{
    public static byte[] Pcm(int channels = 2, int sampleRate = 44100, int bitsPerSample = 16, int dataBytes = 44100 * 2 * 2, ushort formatTag = 1)
    {
        var blockAlign = channels * bitsPerSample / 8;
        var byteRate = sampleRate * blockAlign;
        var fmt = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(0), formatTag);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(8), (uint)byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(12), (ushort)blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), (ushort)bitsPerSample);

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(4 + 8 + fmt.Length + 8 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(fmt.Length);
        writer.Write(fmt);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        writer.Write(new byte[dataBytes]);
        writer.Flush();
        return stream.ToArray();
    }
}
