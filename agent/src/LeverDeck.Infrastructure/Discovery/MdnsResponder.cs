using System.Net;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>A packet to send and where to send it.</summary>
internal sealed record MdnsReply(byte[] Packet, IPEndPoint Destination);

/// <summary>What to do about one received packet: an optional reply and an optional name conflict to report.</summary>
internal sealed record MdnsDecision(MdnsReply? Reply, IPAddress? ConflictingAddress)
{
    public static MdnsDecision None { get; } = new(null, null);
}

/// <summary>
/// The pure part of the mDNS responder (RFC 6762) for one host name: decides the reply to a packet given the current
/// LAN address, and builds announcements and goodbyes. It owns the A record of the name and answers every other type
/// with an NSEC saying A is the only one (§6.1), so resolvers asking for AAAA get a quick negative instead of waiting.
/// No probing (§8.1) and no known-answer suppression (§7.1): the name is ours by configuration, and an extra answer is
/// harmless.
/// </summary>
internal sealed class MdnsResponder(string hostName)
{
    public const int Port = 5353;

    /// <summary>TTL of host-name records (RFC 6762 §10: 120 s for records containing a host name).</summary>
    public const uint Ttl = 120;

    /// <summary>Maximum TTL in a legacy unicast response (RFC 6762 §6.7).</summary>
    public const uint LegacyUnicastTtl = 10;

    public static IPAddress MulticastAddress { get; } = IPAddress.Parse("224.0.0.251");

    public static IPEndPoint MulticastEndPoint { get; } = new(MulticastAddress, Port);

    /// <summary>A QU question still gets a multicast answer when we have not multicast for a quarter of the TTL (§5.4).</summary>
    public static TimeSpan RecentMulticastWindow { get; } = TimeSpan.FromSeconds(Ttl / 4);

    public string HostName => hostName;

    public byte[] Announcement(IPAddress address) =>
        MdnsCodec.Write(Response(0, [], [MdnsCodec.ARecord(hostName, address, Ttl, cacheFlush: true)], [Nsec(Ttl, cacheFlush: true)]));

    public byte[] Goodbye(IPAddress address) =>
        MdnsCodec.Write(Response(0, [], [MdnsCodec.ARecord(hostName, address, 0, cacheFlush: true)], []));

    /// <param name="packet">The datagram as received; anything malformed is ignored.</param>
    /// <param name="source">Where it came from: port 5353 is a full mDNS querier, any other port a legacy resolver.</param>
    /// <param name="address">The LAN IPv4 address the name resolves to.</param>
    /// <param name="multicastRecently">Whether the A record was multicast within <see cref="RecentMulticastWindow"/>.</param>
    public MdnsDecision Decide(ReadOnlySpan<byte> packet, IPEndPoint source, IPAddress address, bool multicastRecently)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(address);

        // §18.3 and §18.11: a non-zero opcode or response code means the message is silently ignored.
        if (!MdnsCodec.TryParse(packet, out var message) || message.Opcode != 0 || message.ResponseCode != 0)
        {
            return MdnsDecision.None;
        }

        if (message.IsResponse)
        {
            var conflict = FindConflict(message, address);
            return conflict is null ? MdnsDecision.None : new MdnsDecision(null, conflict);
        }

        var ours = message.Questions.Where(IsAboutUs).ToList();
        if (ours.Count == 0)
        {
            return MdnsDecision.None;
        }

        var legacy = source.Port != Port;
        var ttl = legacy ? LegacyUnicastTtl : Ttl;
        var cacheFlush = !legacy; // §10.2: never set in legacy unicast responses

        var wantsAddress = ours.Exists(question => question.Type is DnsType.A or DnsType.Any);
        var wantsNegative = ours.Exists(question => question.Type is not (DnsType.A or DnsType.Any));
        var a = MdnsCodec.ARecord(hostName, address, ttl, cacheFlush);
        var nsec = Nsec(ttl, cacheFlush);
        List<MdnsRecord> answers = [];
        List<MdnsRecord> additionals = [];
        (wantsAddress ? answers : additionals).Add(a);
        (wantsNegative ? answers : additionals).Add(nsec);

        if (legacy)
        {
            // §6.7: repeat the query ID and the question, reply straight to the resolver.
            var echoed = message.Questions.Select(question => question with { UnicastResponse = false }).ToList();
            return Reply(Response(message.Id, echoed, answers, additionals), source);
        }

        var unicast = ours.Exists(question => question.UnicastResponse) && multicastRecently && !IsThisHost(source.Address, address);
        return Reply(Response(0, [], answers, additionals), unicast ? source : MulticastEndPoint);
    }

    private bool IsAboutUs(MdnsQuestion question) =>
        question.Class is DnsClass.In or DnsClass.Any
        && string.Equals(question.Name, hostName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Another host's live A record for our name with a different address.</summary>
    private IPAddress? FindConflict(MdnsMessage message, IPAddress address)
    {
        foreach (var record in message.Answers.Concat(message.Additionals))
        {
            if (record is { Type: DnsType.A, Class: DnsClass.In, Data.Length: 4, Ttl: > 0 }
                && string.Equals(record.Name, hostName, StringComparison.OrdinalIgnoreCase))
            {
                var claimed = new IPAddress(record.Data);
                if (!claimed.Equals(address))
                {
                    return claimed;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Unicast to port 5353 on this PC reaches only one of the sockets sharing the port (perhaps not Windows'
    /// resolver), so local queriers always get the multicast answer, which every socket sees through loopback.
    /// </summary>
    private static bool IsThisHost(IPAddress source, IPAddress address) => IPAddress.IsLoopback(source) || source.Equals(address);

    private MdnsRecord Nsec(uint ttl, bool cacheFlush) => MdnsCodec.NsecRecord(hostName, ttl, cacheFlush);

    private static MdnsMessage Response(
        ushort id,
        IReadOnlyList<MdnsQuestion> questions,
        IReadOnlyList<MdnsRecord> answers,
        IReadOnlyList<MdnsRecord> additionals) =>
        new(id, MdnsCodec.AuthoritativeResponseFlags, questions, answers, [], additionals);

    private static MdnsDecision Reply(MdnsMessage response, IPEndPoint destination) =>
        new(new MdnsReply(MdnsCodec.Write(response), destination), null);
}
