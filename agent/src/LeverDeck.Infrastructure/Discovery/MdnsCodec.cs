using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>DNS resource record types the responder reads or writes.</summary>
internal static class DnsType
{
    public const ushort A = 1;
    public const ushort Aaaa = 28;
    public const ushort Nsec = 47;
    public const ushort Any = 255;
}

/// <summary>DNS classes, without the mDNS top bit (QU in questions, cache-flush in records).</summary>
internal static class DnsClass
{
    public const ushort In = 1;
    public const ushort Any = 255;
}

/// <summary>A question; <see cref="UnicastResponse"/> is the mDNS QU bit (RFC 6762 §5.4), split from the class.</summary>
internal readonly record struct MdnsQuestion(string Name, ushort Type, ushort Class, bool UnicastResponse);

/// <summary>A resource record; <see cref="CacheFlush"/> is the mDNS top bit of the class (RFC 6762 §10.2).</summary>
internal sealed record MdnsRecord(string Name, ushort Type, ushort Class, bool CacheFlush, uint Ttl, byte[] Data);

internal sealed record MdnsMessage(
    ushort Id,
    ushort Flags,
    IReadOnlyList<MdnsQuestion> Questions,
    IReadOnlyList<MdnsRecord> Answers,
    IReadOnlyList<MdnsRecord> Authorities,
    IReadOnlyList<MdnsRecord> Additionals)
{
    public bool IsResponse => (Flags & 0x8000) != 0;

    public int Opcode => (Flags >> 11) & 0xF;

    public int ResponseCode => Flags & 0xF;
}

/// <summary>
/// Reads and writes DNS messages (RFC 1035 §4) as used by mDNS. Parsing never throws: anything malformed, truncated or
/// oversized is rejected. Names are read with compression; written names are never compressed (allowed, just larger).
/// </summary>
internal static class MdnsCodec
{
    /// <summary>The largest mDNS message accepted (RFC 6762 §17: 9000 bytes including IP and UDP headers).</summary>
    public const int MaxMessageSize = 9000;

    /// <summary>QR (response) and AA (authoritative answer).</summary>
    public const ushort AuthoritativeResponseFlags = 0x8400;

    private const int HeaderSize = 12;
    private const int QuestionTail = 4; // type + class
    private const int RecordTail = 10; // type + class + TTL + data length
    private const int MaxNameLength = 255;
    private const int MaxLabelLength = 63;
    private const ushort TopBit = 0x8000;

    public static bool TryParse(ReadOnlySpan<byte> packet, [NotNullWhen(true)] out MdnsMessage? message)
    {
        message = null;
        if (packet.Length is < HeaderSize or > MaxMessageSize)
        {
            return false;
        }

        var id = BinaryPrimitives.ReadUInt16BigEndian(packet);
        var flags = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
        var offset = HeaderSize;

        if (!TryReadQuestions(packet, ref offset, BinaryPrimitives.ReadUInt16BigEndian(packet[4..]), out var questions)
            || !TryReadRecords(packet, ref offset, BinaryPrimitives.ReadUInt16BigEndian(packet[6..]), out var answers)
            || !TryReadRecords(packet, ref offset, BinaryPrimitives.ReadUInt16BigEndian(packet[8..]), out var authorities)
            || !TryReadRecords(packet, ref offset, BinaryPrimitives.ReadUInt16BigEndian(packet[10..]), out var additionals))
        {
            return false;
        }

        message = new MdnsMessage(id, flags, questions, answers, authorities, additionals);
        return true;
    }

    public static byte[] Write(MdnsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var writer = new ArrayBufferWriter<byte>(512);
        WriteUInt16(writer, message.Id);
        WriteUInt16(writer, message.Flags);
        WriteUInt16(writer, checked((ushort)message.Questions.Count));
        WriteUInt16(writer, checked((ushort)message.Answers.Count));
        WriteUInt16(writer, checked((ushort)message.Authorities.Count));
        WriteUInt16(writer, checked((ushort)message.Additionals.Count));

        foreach (var question in message.Questions)
        {
            writer.Write(EncodeName(question.Name));
            WriteUInt16(writer, question.Type);
            WriteUInt16(writer, (ushort)(question.Class | (question.UnicastResponse ? TopBit : 0)));
        }

        foreach (var record in message.Answers.Concat(message.Authorities).Concat(message.Additionals))
        {
            writer.Write(EncodeName(record.Name));
            WriteUInt16(writer, record.Type);
            WriteUInt16(writer, (ushort)(record.Class | (record.CacheFlush ? TopBit : 0)));
            BinaryPrimitives.WriteUInt32BigEndian(writer.GetSpan(4), record.Ttl);
            writer.Advance(4);
            WriteUInt16(writer, checked((ushort)record.Data.Length));
            writer.Write(record.Data);
        }

        return writer.WrittenSpan.ToArray();
    }

    public static MdnsRecord ARecord(string name, IPAddress address, uint ttl, bool cacheFlush)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("An A record holds an IPv4 address.", nameof(address));
        }

        return new MdnsRecord(name, DnsType.A, DnsClass.In, cacheFlush, ttl, address.GetAddressBytes());
    }

    /// <summary>
    /// An NSEC record asserting that <paramref name="name"/> has an A record and nothing else (RFC 6762 §6.1): next
    /// domain name = the name itself, then window block 0 with a one-byte bitmap where only bit 1 (type A) is set.
    /// </summary>
    public static MdnsRecord NsecRecord(string name, uint ttl, bool cacheFlush)
    {
        var next = EncodeName(name);
        var data = new byte[next.Length + 3];
        next.CopyTo(data, 0);
        data[next.Length] = 0; // window block 0 (types 0-255)
        data[next.Length + 1] = 1; // bitmap length
        data[next.Length + 2] = 0x40; // type 1 (A) is the second most significant bit
        return new MdnsRecord(name, DnsType.Nsec, DnsClass.In, cacheFlush, ttl, data);
    }

    /// <summary>The name as uncompressed labels ("a.local" → 1 'a' 5 'local' 0).</summary>
    public static byte[] EncodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var bytes = new List<byte>(name.Length + 2);
        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var encoded = Encoding.UTF8.GetBytes(label);
            if (encoded.Length > MaxLabelLength)
            {
                throw new ArgumentException($"The label '{label}' is longer than {MaxLabelLength} bytes.", nameof(name));
            }

            bytes.Add((byte)encoded.Length);
            bytes.AddRange(encoded);
        }

        bytes.Add(0);
        return bytes.Count > MaxNameLength
            ? throw new ArgumentException($"The name is longer than {MaxNameLength} bytes.", nameof(name))
            : [.. bytes];
    }

    private static bool TryReadQuestions(ReadOnlySpan<byte> packet, ref int offset, int count, out List<MdnsQuestion> questions)
    {
        // Every question takes at least 5 bytes: never trust the count for the allocation.
        questions = new List<MdnsQuestion>(Math.Min(count, (packet.Length - offset) / (1 + QuestionTail)));
        for (var index = 0; index < count; index++)
        {
            if (!TryReadName(packet, ref offset, out var name) || offset + QuestionTail > packet.Length)
            {
                return false;
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]);
            var rawClass = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 2)..]);
            offset += QuestionTail;
            questions.Add(new MdnsQuestion(name, type, (ushort)(rawClass & ~TopBit), (rawClass & TopBit) != 0));
        }

        return true;
    }

    private static bool TryReadRecords(ReadOnlySpan<byte> packet, ref int offset, int count, out List<MdnsRecord> records)
    {
        records = new List<MdnsRecord>(Math.Min(count, (packet.Length - offset) / (1 + RecordTail)));
        for (var index = 0; index < count; index++)
        {
            if (!TryReadName(packet, ref offset, out var name) || offset + RecordTail > packet.Length)
            {
                return false;
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]);
            var rawClass = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 2)..]);
            var ttl = BinaryPrimitives.ReadUInt32BigEndian(packet[(offset + 4)..]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 8)..]);
            offset += RecordTail;
            if (offset + length > packet.Length)
            {
                return false;
            }

            var data = packet.Slice(offset, length).ToArray();
            offset += length;
            records.Add(new MdnsRecord(name, type, (ushort)(rawClass & ~TopBit), (rawClass & TopBit) != 0, ttl, data));
        }

        return true;
    }

    /// <summary>
    /// Reads a possibly compressed name starting at <paramref name="offset"/> and moves it past the name. Compression
    /// pointers must point strictly backwards, which rules out loops; reserved label types are rejected.
    /// </summary>
    private static bool TryReadName(ReadOnlySpan<byte> packet, ref int offset, out string name)
    {
        name = string.Empty;
        var builder = new StringBuilder();
        var position = offset;
        var resumeAt = -1; // where the stream continues after the first pointer
        var encodedLength = 1; // the terminating zero

        while (true)
        {
            if (position >= packet.Length)
            {
                return false;
            }

            var length = packet[position];
            if (length == 0)
            {
                position++;
                break;
            }

            switch (length & 0xC0)
            {
                case 0xC0:
                    if (position + 1 >= packet.Length)
                    {
                        return false;
                    }

                    var target = ((length & 0x3F) << 8) | packet[position + 1];
                    if (target >= position)
                    {
                        return false;
                    }

                    if (resumeAt < 0)
                    {
                        resumeAt = position + 2;
                    }

                    position = target;
                    break;

                case 0x00:
                    encodedLength += length + 1;
                    if (encodedLength > MaxNameLength || position + 1 + length > packet.Length)
                    {
                        return false;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append('.');
                    }

                    builder.Append(Encoding.UTF8.GetString(packet.Slice(position + 1, length)));
                    position += 1 + length;
                    break;

                default:
                    return false; // 0x40 and 0x80 label types are reserved or obsolete
            }
        }

        offset = resumeAt >= 0 ? resumeAt : position;
        name = builder.ToString();
        return true;
    }

    private static void WriteUInt16(ArrayBufferWriter<byte> writer, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(writer.GetSpan(2), value);
        writer.Advance(2);
    }
}
