using System.Net;
using LeverDeck.Infrastructure.Discovery;

namespace LeverDeck.Infrastructure.Tests.Discovery;

public sealed class MdnsResponderTests
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.35");
    private static readonly IPEndPoint Phone = new(IPAddress.Parse("192.168.1.50"), MdnsResponder.Port);

    private readonly MdnsResponder _responder = new("leverdeck.local");

    private static byte[] Query(params MdnsQuestion[] questions) => Query(id: 0, questions);

    private static byte[] Query(ushort id, params MdnsQuestion[] questions) =>
        MdnsCodec.Write(new MdnsMessage(id, 0, questions, [], [], []));

    private static MdnsQuestion Ask(string name = "leverdeck.local", ushort type = DnsType.A, bool qu = false, ushort @class = DnsClass.In) =>
        new(name, type, @class, qu);

    private MdnsDecision Decide(byte[] packet, IPEndPoint? source = null, bool multicastRecently = false) =>
        _responder.Decide(packet, source ?? Phone, Lan, multicastRecently);

    private static MdnsMessage Parse(MdnsReply? reply)
    {
        Assert.NotNull(reply);
        Assert.True(MdnsCodec.TryParse(reply.Packet, out var message));
        return message;
    }

    [Fact]
    public void ARecordQuery_GetsTheGoldenMulticastAnswer()
    {
        var decision = Decide(Convert.FromHexString(MdnsCodecTests.QueryHex));

        Assert.NotNull(decision.Reply);
        Assert.Equal(MdnsResponder.MulticastEndPoint, decision.Reply.Destination);
        Assert.Equal(MdnsCodecTests.ResponseHex, Convert.ToHexString(decision.Reply.Packet));
    }

    [Fact]
    public void NameMatching_IgnoresCase()
    {
        var answer = Assert.Single(Parse(Decide(Query(Ask("LeverDeck.LOCAL"))).Reply).Answers);

        Assert.Equal(DnsType.A, answer.Type);
    }

    [Fact]
    public void AnyQuery_GetsTheARecord()
    {
        var answer = Assert.Single(Parse(Decide(Query(Ask(type: DnsType.Any))).Reply).Answers);

        Assert.Equal(DnsType.A, answer.Type);
    }

    [Fact]
    public void AnyClass_IsAnswered()
    {
        Assert.NotNull(Decide(Query(Ask(@class: DnsClass.Any))).Reply);
    }

    [Fact]
    public void AaaaQuery_GetsAnNsecAndTheARecordAsAdditional()
    {
        var message = Parse(Decide(Query(Ask(type: DnsType.Aaaa))).Reply);

        var nsec = Assert.Single(message.Answers);
        Assert.Equal(DnsType.Nsec, nsec.Type);
        Assert.Equal(MdnsCodec.NsecRecord("leverdeck.local", MdnsResponder.Ttl, cacheFlush: true).Data, nsec.Data);
        var additional = Assert.Single(message.Additionals);
        Assert.Equal(DnsType.A, additional.Type);
    }

    [Fact]
    public void AAndAaaaInOneQuery_AnswerBothWithNothingAdditional()
    {
        var message = Parse(Decide(Query(Ask(type: DnsType.A), Ask(type: DnsType.Aaaa))).Reply);

        Assert.Equal(new[] { DnsType.A, DnsType.Nsec }, message.Answers.Select(answer => answer.Type));
        Assert.Empty(message.Additionals);
    }

    [Theory]
    [InlineData("other.local")]
    [InlineData("leverdeck.lan")]
    [InlineData("leverdeck")]
    [InlineData("xleverdeck.local")]
    public void OtherNames_GetNothing(string name)
    {
        Assert.Equal(MdnsDecision.None, Decide(Query(Ask(name))));
    }

    [Fact]
    public void OtherClasses_GetNothing()
    {
        Assert.Equal(MdnsDecision.None, Decide(Query(Ask(@class: 3)))); // CHAOS
    }

    [Fact]
    public void OnlyTheQuestionsAboutUsAreAnswered()
    {
        var message = Parse(Decide(Query(Ask("printer.local"), Ask())).Reply);

        Assert.Single(message.Answers);
    }

    [Fact]
    public void Responses_AreNeverAnswered()
    {
        var packet = MdnsCodec.Write(new MdnsMessage(0, MdnsCodec.AuthoritativeResponseFlags, [Ask()], [], [], []));

        Assert.Null(Decide(packet).Reply);
    }

    [Fact]
    public void NonZeroOpcode_IsIgnored()
    {
        var packet = MdnsCodec.Write(new MdnsMessage(0, 0x2800, [Ask()], [], [], [])); // opcode 5 (UPDATE)

        Assert.Equal(MdnsDecision.None, Decide(packet));
    }

    [Fact]
    public void NonZeroResponseCode_IsIgnored()
    {
        var packet = MdnsCodec.Write(new MdnsMessage(0, 0x0003, [Ask()], [], [], []));

        Assert.Equal(MdnsDecision.None, Decide(packet));
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("000000000001000000000000C00C00010001")]
    public void MalformedPackets_AreIgnored(string hex)
    {
        Assert.Equal(MdnsDecision.None, Decide(Convert.FromHexString(hex)));
    }

    [Fact]
    public void QuQuery_IsAnsweredByUnicastWhenWeMulticastRecently()
    {
        var reply = Decide(Query(Ask(qu: true)), multicastRecently: true).Reply;

        Assert.NotNull(reply);
        Assert.Equal(Phone, reply.Destination);
        var message = Parse(reply);
        Assert.Equal(0, message.Id);
        Assert.Empty(message.Questions);
        Assert.True(Assert.Single(message.Answers).CacheFlush);
    }

    [Fact]
    public void QuQuery_IsMulticastWhenWeHaveNotMulticastRecently()
    {
        // RFC 6762 §5.4: a record not multicast within a quarter of its TTL is multicast even for a QU question.
        var reply = Decide(Query(Ask(qu: true)), multicastRecently: false).Reply;

        Assert.Equal(MdnsResponder.MulticastEndPoint, reply?.Destination);
    }

    [Theory]
    [InlineData("192.168.1.35")]
    [InlineData("127.0.0.1")]
    public void QuQueryFromThisPc_IsMulticast(string source)
    {
        // Unicast to port 5353 on this PC reaches one arbitrary socket sharing the port, maybe not Windows' resolver.
        var reply = Decide(Query(Ask(qu: true)), new IPEndPoint(IPAddress.Parse(source), MdnsResponder.Port), multicastRecently: true).Reply;

        Assert.Equal(MdnsResponder.MulticastEndPoint, reply?.Destination);
    }

    [Fact]
    public void LegacyUnicastQuery_EchoesTheIdAndQuestionWithAShortTtl()
    {
        var resolver = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 50_123);

        var reply = Decide(Query(0x1234, Ask(qu: true)), resolver).Reply;

        Assert.NotNull(reply);
        Assert.Equal(resolver, reply.Destination);
        var message = Parse(reply);
        Assert.Equal(0x1234, message.Id);
        Assert.True(message.IsResponse);
        Assert.Equal(Ask(qu: false), Assert.Single(message.Questions));
        var answer = Assert.Single(message.Answers);
        Assert.Equal(MdnsResponder.LegacyUnicastTtl, answer.Ttl);
        Assert.False(answer.CacheFlush); // RFC 6762 §6.7
        Assert.Equal(Lan.GetAddressBytes(), answer.Data);
    }

    [Fact]
    public void Announcement_IsTheGoldenResponse()
    {
        Assert.Equal(MdnsCodecTests.ResponseHex, Convert.ToHexString(_responder.Announcement(Lan)));
    }

    [Fact]
    public void Goodbye_HasTtlZero()
    {
        Assert.True(MdnsCodec.TryParse(_responder.Goodbye(Lan), out var message));

        var answer = Assert.Single(message.Answers);
        Assert.Equal(DnsType.A, answer.Type);
        Assert.Equal(0u, answer.Ttl);
        Assert.Equal(Lan.GetAddressBytes(), answer.Data);
    }

    private static byte[] ResponseClaiming(string address, uint ttl = 120, string name = "leverdeck.local") =>
        MdnsCodec.Write(new MdnsMessage(
            0, MdnsCodec.AuthoritativeResponseFlags, [], [MdnsCodec.ARecord(name, IPAddress.Parse(address), ttl, true)], [], []));

    [Fact]
    public void AnotherHostAnsweringForOurName_IsAConflict()
    {
        Assert.Equal(new MdnsDecision(null, IPAddress.Parse("192.168.1.99")), Decide(ResponseClaiming("192.168.1.99")));
    }

    [Fact]
    public void OurOwnLoopedBackAnnouncement_IsNotAConflict()
    {
        Assert.Equal(MdnsDecision.None, Decide(_responder.Announcement(Lan)));
    }

    [Fact]
    public void AnotherHostsGoodbyeOrOtherName_IsNotAConflict()
    {
        Assert.Equal(MdnsDecision.None, Decide(ResponseClaiming("192.168.1.99", ttl: 0)));
        Assert.Equal(MdnsDecision.None, Decide(ResponseClaiming("192.168.1.99", name: "printer.local")));
    }
}
