using System.Net;
using LeverDeck.Infrastructure.Discovery;

namespace LeverDeck.Infrastructure.Tests.Discovery;

public sealed class MdnsCodecTests
{
    // "leverdeck.local" as uncompressed labels: 9 "leverdeck" 5 "local" 0.
    internal const string NameHex = "096C657665726465636B056C6F63616C00";

    // Standard query, ID 0, one question: leverdeck.local A IN (QM).
    internal const string QueryHex = "000000000001000000000000" + NameHex + "00010001";

    // Authoritative multicast response, ID 0: A 192.168.1.35 (cache-flush, TTL 120) plus an NSEC in the additional
    // section stating that A is the only type the name has.
    internal const string ResponseHex =
        "000084000000000100000001"
        + NameHex + "0001" + "8001" + "00000078" + "0004" + "C0A80123"
        + NameHex + "002F" + "8001" + "00000078" + "0014" + NameHex + "000140";

    [Fact]
    public void TryParse_ReadsTheGoldenQuery()
    {
        Assert.True(MdnsCodec.TryParse(Convert.FromHexString(QueryHex), out var message));

        Assert.False(message.IsResponse);
        Assert.Equal(0, message.Opcode);
        var question = Assert.Single(message.Questions);
        Assert.Equal(new MdnsQuestion("leverdeck.local", DnsType.A, DnsClass.In, UnicastResponse: false), question);
        Assert.Empty(message.Answers);
    }

    [Fact]
    public void TryParse_SplitsTheQuBitFromTheClass()
    {
        var packet = Convert.FromHexString("000000000001000000000000" + NameHex + "00018001");

        Assert.True(MdnsCodec.TryParse(packet, out var message));

        var question = Assert.Single(message.Questions);
        Assert.True(question.UnicastResponse);
        Assert.Equal(DnsClass.In, question.Class);
    }

    [Fact]
    public void TryParse_FollowsNameCompressionAcrossSeveralQuestions()
    {
        // Q1 at offset 12: leverdeck.local A. Its "local" label starts at offset 22 (0x16).
        // Q2: "other" + pointer to 0x16 -> other.local AAAA. Q3: pointer to 0x0C -> leverdeck.local ANY.
        var packet = Convert.FromHexString(
            "000000000003000000000000"
            + NameHex + "00010001"
            + "056F74686572C016" + "001C0001"
            + "C00C" + "00FF8001");

        Assert.True(MdnsCodec.TryParse(packet, out var message));

        MdnsQuestion[] expected =
        [
            new("leverdeck.local", DnsType.A, DnsClass.In, false),
            new("other.local", DnsType.Aaaa, DnsClass.In, false),
            new("leverdeck.local", DnsType.Any, DnsClass.In, true),
        ];
        Assert.Equal(expected, message.Questions);
    }

    [Fact]
    public void TryParse_ReadsResourceRecords()
    {
        Assert.True(MdnsCodec.TryParse(Convert.FromHexString(ResponseHex), out var message));

        Assert.True(message.IsResponse);
        var answer = Assert.Single(message.Answers);
        Assert.Equal("leverdeck.local", answer.Name);
        Assert.Equal(DnsType.A, answer.Type);
        Assert.Equal(DnsClass.In, answer.Class);
        Assert.True(answer.CacheFlush);
        Assert.Equal(120u, answer.Ttl);
        Assert.Equal(new byte[] { 192, 168, 1, 35 }, answer.Data);
        var nsec = Assert.Single(message.Additionals);
        Assert.Equal(DnsType.Nsec, nsec.Type);
    }

    public static TheoryData<string, string> MalformedPackets => new()
    {
        { "shorter than the header", "00000000000100" },
        { "question announced but missing", "000000000001000000000000" },
        { "label runs past the end", "000000000001000000000000" + "096C6576" },
        { "question without type and class", "000000000001000000000000" + NameHex + "0001" },
        { "pointer to itself", "000000000001000000000000" + "C00C" + "00010001" },
        { "forward pointer", "000000000001000000000000" + "C010" + "00010001" + "0000" },
        { "pointer cut in half", "000000000001000000000000" + "C0" },
        { "reserved label type", "000000000001000000000000" + "4100" + "00010001" },
        { "unterminated name", "000000000001000000000000" + NameHex[..^2] },
        { "record data past the end", "000084000000000100000000" + NameHex + "00018001000000780004C0A8" },
        { "record header cut", "000084000000000100000000" + NameHex + "000180010000" },
        { "name longer than 255 bytes", "000000000001000000000000" + string.Concat(Enumerable.Repeat("3F" + new string('6', 126), 5)) + "00" + "00010001" },
    };

    [Theory]
    [MemberData(nameof(MalformedPackets))]
    public void TryParse_RejectsMalformedPackets(string because, string hex)
    {
        Assert.False(MdnsCodec.TryParse(Convert.FromHexString(hex), out _), because);
    }

    [Fact]
    public void TryParse_RejectsOversizedPackets()
    {
        var packet = new byte[MdnsCodec.MaxMessageSize + 1];
        Convert.FromHexString(QueryHex).CopyTo(packet, 0);

        Assert.False(MdnsCodec.TryParse(packet, out _));
    }

    [Fact]
    public void TryParse_RejectsHugeCountsWithoutAllocatingForThem()
    {
        Assert.False(MdnsCodec.TryParse(Convert.FromHexString("0000" + "0000" + "FFFF" + "FFFF" + "FFFF" + "FFFF"), out _));
    }

    [Fact]
    public void Write_ProducesTheGoldenResponse()
    {
        var message = new MdnsMessage(
            Id: 0,
            Flags: MdnsCodec.AuthoritativeResponseFlags,
            Questions: [],
            Answers: [MdnsCodec.ARecord("leverdeck.local", IPAddress.Parse("192.168.1.35"), ttl: 120, cacheFlush: true)],
            Authorities: [],
            Additionals: [MdnsCodec.NsecRecord("leverdeck.local", ttl: 120, cacheFlush: true)]);

        Assert.Equal(ResponseHex, Convert.ToHexString(MdnsCodec.Write(message)));
    }

    [Fact]
    public void Write_ProducesTheGoldenQuery()
    {
        var message = new MdnsMessage(0, 0, [new MdnsQuestion("leverdeck.local", DnsType.A, DnsClass.In, false)], [], [], []);

        Assert.Equal(QueryHex, Convert.ToHexString(MdnsCodec.Write(message)));
    }

    [Fact]
    public void Write_SetsTheQuBitOnQuestions()
    {
        var message = new MdnsMessage(0, 0, [new MdnsQuestion("leverdeck.local", DnsType.A, DnsClass.In, true)], [], [], []);

        Assert.EndsWith("00018001", Convert.ToHexString(MdnsCodec.Write(message)), StringComparison.Ordinal);
    }

    [Fact]
    public void NsecRecord_ListsOnlyTheARecordType()
    {
        var nsec = MdnsCodec.NsecRecord("leverdeck.local", ttl: 120, cacheFlush: true);

        Assert.Equal(DnsType.Nsec, nsec.Type);
        Assert.Equal(NameHex + "000140", Convert.ToHexString(nsec.Data));
    }

    [Fact]
    public void ARecord_WithTtlZeroIsAGoodbye()
    {
        var goodbye = MdnsCodec.ARecord("leverdeck.local", IPAddress.Parse("10.0.0.7"), ttl: 0, cacheFlush: true);

        Assert.Equal(0u, goodbye.Ttl);
        Assert.Equal(new byte[] { 10, 0, 0, 7 }, goodbye.Data);
    }

    [Fact]
    public void ARecord_RejectsIpv6()
    {
        Assert.Throws<ArgumentException>(() => MdnsCodec.ARecord("leverdeck.local", IPAddress.IPv6Loopback, 120, true));
    }
}
