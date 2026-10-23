using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeverDeck.Infrastructure.Tests.Discovery;

/// <summary>Records what the responder sends and feeds it datagrams; never touches the network.</summary>
internal sealed class FakeMdnsTransport(IPAddress interfaceAddress) : IMdnsTransport
{
    private readonly Channel<(byte[] Packet, IPEndPoint Source)> _incoming = Channel.CreateUnbounded<(byte[], IPEndPoint)>();
    private readonly List<MdnsReply> _sent = [];

    public IPAddress InterfaceAddress { get; } = interfaceAddress;

    public bool Disposed { get; private set; }

    public IReadOnlyList<MdnsReply> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public void Deliver(byte[] packet, IPEndPoint source) => _incoming.Writer.TryWrite((packet, source));

    public async Task<IReadOnlyList<MdnsReply>> WaitForSentAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Sent.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        return Sent;
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> packet, IPEndPoint destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        lock (_sent)
        {
            _sent.Add(new MdnsReply(packet.ToArray(), destination));
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<MdnsDatagram> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (packet, source) = await _incoming.Reader.ReadAsync(cancellationToken);
        packet.CopyTo(buffer);
        return new MdnsDatagram(packet.Length, source);
    }

    public void Dispose()
    {
        Disposed = true;
    }
}

public sealed class MdnsHostNameResponderTests
{
    private static readonly IPAddress First = IPAddress.Parse("192.168.1.35");
    private static readonly IPAddress Second = IPAddress.Parse("192.168.1.77");

    private readonly List<FakeMdnsTransport> _transports = [];
    private IPAddress? _address = First;

    private MdnsHostNameResponder NewResponder(string hostName = "leverdeck.local", ILogger<MdnsHostNameResponder>? logger = null) =>
        new(
            logger ?? NullLogger<MdnsHostNameResponder>.Instance,
            TimeProvider.System,
            AgentHostNames.Of(hostName),
            () => _address,
            address =>
            {
                var transport = new FakeMdnsTransport(address);
                _transports.Add(transport);
                return transport;
            },
            announcementInterval: TimeSpan.FromMilliseconds(20));

    private static IPAddress AddressOf(MdnsReply reply, out uint ttl)
    {
        Assert.True(MdnsCodec.TryParse(reply.Packet, out var message));
        var answer = Assert.Single(message.Answers);
        ttl = answer.Ttl;
        return new IPAddress(answer.Data);
    }

    [Fact]
    public async Task Advertise_JoinsOnTheLanAddressAndAnnouncesTwice()
    {
        using var responder = NewResponder();

        await responder.AdvertiseAsync(CancellationToken.None);

        var transport = Assert.Single(_transports);
        Assert.Equal(First, transport.InterfaceAddress);
        var sent = await transport.WaitForSentAsync(2);
        Assert.Equal(2, sent.Count);
        Assert.All(sent, reply =>
        {
            Assert.Equal(MdnsResponder.MulticastEndPoint, reply.Destination);
            Assert.Equal(MdnsCodecTests.ResponseHex, Convert.ToHexString(reply.Packet));
        });
        await responder.WithdrawAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Advertise_IsIdempotent()
    {
        using var responder = NewResponder();

        await responder.AdvertiseAsync(CancellationToken.None);
        await responder.AdvertiseAsync(CancellationToken.None);

        Assert.Single(_transports);
        await responder.WithdrawAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Queries_AreAnswered()
    {
        using var responder = NewResponder();
        await responder.AdvertiseAsync(CancellationToken.None);
        var transport = _transports[0];
        await transport.WaitForSentAsync(2);

        transport.Deliver(Convert.FromHexString(MdnsCodecTests.QueryHex), new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353));
        transport.Deliver([1, 2, 3], new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353)); // garbage is ignored
        transport.Deliver(Convert.FromHexString(MdnsCodecTests.QueryHex), new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353));

        var sent = await transport.WaitForSentAsync(4);
        Assert.Equal(4, sent.Count);
        Assert.Equal(MdnsCodecTests.ResponseHex, Convert.ToHexString(sent[3].Packet));
        await responder.WithdrawAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Queries_AreAnsweredForThisPcsNameOnly()
    {
        using var responder = NewResponder("leverdeck-studio.local");
        await responder.AdvertiseAsync(CancellationToken.None);
        var transport = _transports[0];
        await transport.WaitForSentAsync(2);
        var from = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353);

        transport.Deliver(Convert.FromHexString(MdnsCodecTests.QueryHex), from); // leverdeck.local: another PC's name
        transport.Deliver(Query("leverdeck-studio.local"), from);

        var sent = await transport.WaitForSentAsync(3);
        Assert.Equal(3, sent.Count);
        Assert.True(MdnsCodec.TryParse(sent[2].Packet, out var answer));
        Assert.Equal("leverdeck-studio.local", Assert.Single(answer.Answers).Name);
        await responder.WithdrawAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AnotherHostAnsweringForOurName_IsLoggedWithHowToRenameThisPc()
    {
        var logger = new RecordingLogger<MdnsHostNameResponder>();
        using var responder = NewResponder(logger: logger);
        await responder.AdvertiseAsync(CancellationToken.None);
        var transport = _transports[0];
        await transport.WaitForSentAsync(2);

        var claim = MdnsCodec.Write(new MdnsMessage(
            0, MdnsCodec.AuthoritativeResponseFlags, [], [MdnsCodec.ARecord("leverdeck.local", Second, 120, true)], [], []));
        transport.Deliver(claim, new IPEndPoint(Second, 5353));

        var warning = await logger.WaitForAsync(LogLevel.Warning);
        Assert.Contains("192.168.1.77", warning, StringComparison.Ordinal);
        Assert.Contains("LeverDeck__Mdns__HostName", warning, StringComparison.Ordinal);
        await responder.WithdrawAsync(CancellationToken.None);
    }

    private static byte[] Query(string name) =>
        MdnsCodec.Write(new MdnsMessage(0, 0, [new MdnsQuestion(name, DnsType.A, DnsClass.In, UnicastResponse: false)], [], [], []));

    [Fact]
    public async Task Withdraw_SaysGoodbyeAndClosesTheTransport()
    {
        using var responder = NewResponder();
        await responder.AdvertiseAsync(CancellationToken.None);
        var transport = _transports[0];
        await transport.WaitForSentAsync(2);

        await responder.WithdrawAsync(CancellationToken.None);

        var goodbye = transport.Sent[^1];
        Assert.Equal(MdnsResponder.MulticastEndPoint, goodbye.Destination);
        Assert.Equal(First, AddressOf(goodbye, out var ttl));
        Assert.Equal(0u, ttl);
        Assert.True(transport.Disposed);
    }

    [Fact]
    public async Task Withdraw_WithoutAdvertising_DoesNothing()
    {
        using var responder = NewResponder();

        await responder.WithdrawAsync(CancellationToken.None);

        Assert.Empty(_transports);
    }

    [Fact]
    public async Task AddressChange_GoodbyeForTheOldAddressThenAnnouncementsForTheNewOne()
    {
        using var responder = NewResponder();
        await responder.AdvertiseAsync(CancellationToken.None);
        await _transports[0].WaitForSentAsync(2);

        // ServiceAdvertisementHostedService reacts to an address change with Withdraw + Advertise.
        _address = Second;
        await responder.WithdrawAsync(CancellationToken.None);
        await responder.AdvertiseAsync(CancellationToken.None);

        Assert.Equal(First, AddressOf(_transports[0].Sent[^1], out var goodbyeTtl));
        Assert.Equal(0u, goodbyeTtl);
        var renewed = Assert.Single(_transports, transport => transport.InterfaceAddress.Equals(Second));
        var announcements = await renewed.WaitForSentAsync(2);
        Assert.Equal(2, announcements.Count);
        Assert.All(announcements, reply =>
        {
            Assert.Equal(Second, AddressOf(reply, out var ttl));
            Assert.Equal(MdnsResponder.Ttl, ttl);
        });
        await responder.WithdrawAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Advertise_WithoutALanAddress_Fails()
    {
        _address = null;
        using var responder = NewResponder();

        await Assert.ThrowsAsync<InvalidOperationException>(() => responder.AdvertiseAsync(CancellationToken.None));

        Assert.Empty(_transports);
    }
}

public sealed class UdpMdnsTransportTests
{
    [Fact]
    public void Socket_SharesPort5353AndLoopsMulticastBack()
    {
        // Configured but never bound: nothing is sent or received.
        using var socket = UdpMdnsTransport.CreateSocket();

        Assert.False(socket.ExclusiveAddressUse);
        Assert.NotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)!);
        Assert.NotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback)!);
        Assert.Equal(255, (int)socket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive)!);
        Assert.Equal(255, socket.Ttl);
        Assert.Null(socket.LocalEndPoint);
    }
}

public sealed class CompositeServiceAdvertiserTests
{
    private sealed class Recording(string name, List<string> calls, bool fail = false) : IServiceAdvertiser
    {
        public Task AdvertiseAsync(CancellationToken cancellationToken) => Record("advertise");

        public Task WithdrawAsync(CancellationToken cancellationToken) => Record("withdraw");

        private Task Record(string verb)
        {
            calls.Add($"{verb} {name}");
            return fail ? Task.FromException(new InvalidOperationException(name)) : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CallsEveryAdvertiserInOrder()
    {
        var calls = new List<string>();
        var composite = new CompositeServiceAdvertiser([new Recording("host", calls), new Recording("service", calls)]);

        await composite.AdvertiseAsync(CancellationToken.None);
        await composite.WithdrawAsync(CancellationToken.None);

        Assert.Equal(["advertise host", "advertise service", "withdraw host", "withdraw service"], calls);
    }

    [Fact]
    public async Task OneFailure_DoesNotSkipTheOthers()
    {
        var calls = new List<string>();
        var composite = new CompositeServiceAdvertiser([new Recording("host", calls, fail: true), new Recording("service", calls)]);

        var failure = await Assert.ThrowsAsync<AggregateException>(() => composite.AdvertiseAsync(CancellationToken.None));

        Assert.Equal(["advertise host", "advertise service"], calls);
        Assert.Equal("host", Assert.Single(failure.InnerExceptions).Message);
    }
}
