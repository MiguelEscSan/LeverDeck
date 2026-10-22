using System.Net;
using System.Net.Sockets;
using LeverDeck.Application.Common.Ports;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// Answers mDNS queries for <see cref="DnsServiceAdvertiser.HostName"/> with the LAN IPv4 address, because the native
/// <c>DnsServiceRegister</c> publishes the service records but Windows does not answer A queries for its target host.
/// Advertising opens a UDP 5353 socket on the LAN interface (<see cref="LanAddressSelector"/>), sends two unsolicited
/// announcements one second apart (RFC 6762 §8.3) and answers until withdrawn; withdrawing sends a goodbye (TTL 0).
/// <see cref="ServiceAdvertisementHostedService"/> withdraws and re-advertises on address changes, which moves the
/// socket to the new interface and announces the new address.
/// </summary>
public sealed partial class MdnsHostNameResponder : IServiceAdvertiser, IDisposable
{
    private static readonly TimeSpan DefaultAnnouncementInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ReceiveErrorBackoff = TimeSpan.FromMilliseconds(200);

    private readonly ILogger<MdnsHostNameResponder> _logger;
    private readonly TimeProvider _time;
    private readonly Func<IPAddress?> _selectAddress;
    private readonly Func<IPAddress, IMdnsTransport> _openTransport;
    private readonly TimeSpan _announcementInterval;
    private readonly MdnsResponder _responder = new(DnsServiceAdvertiser.HostName);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Session? _session;

    public MdnsHostNameResponder(ILogger<MdnsHostNameResponder> logger, TimeProvider time)
        : this(
            logger,
            time,
            () => LanAddressSelector.Select(LanAddressSelector.FromSystem()),
            UdpMdnsTransport.Open,
            DefaultAnnouncementInterval)
    {
    }

    internal MdnsHostNameResponder(
        ILogger<MdnsHostNameResponder> logger,
        TimeProvider time,
        Func<IPAddress?> selectAddress,
        Func<IPAddress, IMdnsTransport> openTransport,
        TimeSpan announcementInterval)
    {
        _logger = logger;
        _time = time;
        _selectAddress = selectAddress;
        _openTransport = openTransport;
        _announcementInterval = announcementInterval;
    }

    public async Task AdvertiseAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null)
            {
                return;
            }

            var address = _selectAddress()
                ?? throw new InvalidOperationException($"No LAN IPv4 address is available to answer for {_responder.HostName}.");
            var session = new Session(_openTransport(address), address);
            _session = session;
            session.Receiving = ReceiveLoopAsync(session);
            await SendAsync(session, new MdnsReply(_responder.Announcement(address), MdnsResponder.MulticastEndPoint), cancellationToken)
                .ConfigureAwait(false);
            session.Announcing = AnnounceAgainAsync(session);
            LogAnswering(_responder.HostName, address);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WithdrawAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not { } session)
            {
                return;
            }

            _session = null;
            await session.Stopping.CancelAsync().ConfigureAwait(false);
            await session.Announcing.ConfigureAwait(false);
            await session.Receiving.ConfigureAwait(false);
            await SendAsync(session, new MdnsReply(_responder.Goodbye(session.Address), MdnsResponder.MulticastEndPoint), CancellationToken.None)
                .ConfigureAwait(false);
            session.Dispose();
            LogStopped(_responder.HostName);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _gate.Dispose();
    }

    private async Task AnnounceAgainAsync(Session session)
    {
        try
        {
            await Task.Delay(_announcementInterval, _time, session.Stopping.Token).ConfigureAwait(false);
            await SendAsync(session, new MdnsReply(_responder.Announcement(session.Address), MdnsResponder.MulticastEndPoint), session.Stopping.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Withdrawn before the second announcement.
        }
    }

    private async Task ReceiveLoopAsync(Session session)
    {
        await Task.Yield();
        var buffer = new byte[MdnsCodec.MaxMessageSize];
        var token = session.Stopping.Token;
        while (!token.IsCancellationRequested)
        {
            MdnsDatagram datagram;
            try
            {
                datagram = await session.Transport.ReceiveAsync(buffer, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException exception)
            {
                // Oversized datagrams and transient network errors: skip, without spinning on a persistent error.
                LogReceiveFailed(exception.SocketErrorCode);
                if (!await DelayAsync(ReceiveErrorBackoff, token).ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            var decision = Decide(session, buffer.AsSpan(0, datagram.Length), datagram.Source);
            if (decision.ConflictingAddress is { } other && session.ReportedConflicts.Add(other))
            {
                LogConflict(_responder.HostName, other, session.Address);
            }

            if (decision.Reply is { } reply)
            {
                await SendAsync(session, reply, token).ConfigureAwait(false);
            }
        }
    }

    private MdnsDecision Decide(Session session, ReadOnlySpan<byte> packet, IPEndPoint source)
    {
        var multicastRecently = _time.GetUtcNow() - session.LastMulticast < MdnsResponder.RecentMulticastWindow;
        return _responder.Decide(packet, source, session.Address, multicastRecently);
    }

    /// <summary>Sends and logs a failure instead of throwing: one lost datagram must not stop the responder.</summary>
    private async Task SendAsync(Session session, MdnsReply reply, CancellationToken cancellationToken)
    {
        try
        {
            await session.Transport.SendAsync(reply.Packet, reply.Destination, cancellationToken).ConfigureAwait(false);
            if (reply.Destination.Equals(MdnsResponder.MulticastEndPoint))
            {
                session.LastMulticast = _time.GetUtcNow();
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            LogSendFailed(reply.Destination, exception);
        }
    }

    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Answering mDNS queries for {HostName} with {Address}.")]
    private partial void LogAnswering(string hostName, IPAddress address);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped answering mDNS queries for {HostName}.")]
    private partial void LogStopped(string hostName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Another host answers mDNS queries for {HostName} with {OtherAddress}; this PC answers with {Address}. Devices may reach the wrong host.")]
    private partial void LogConflict(string hostName, IPAddress otherAddress, IPAddress address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "An mDNS receive failed with {Error}.")]
    private partial void LogReceiveFailed(SocketError error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending an mDNS packet to {Destination} failed.")]
    private partial void LogSendFailed(IPEndPoint destination, Exception exception);

    /// <summary>One advertisement: the socket on one interface and the address it answers with.</summary>
    private sealed class Session(IMdnsTransport transport, IPAddress address) : IDisposable
    {
        public IMdnsTransport Transport { get; } = transport;

        public IPAddress Address { get; } = address;

        public CancellationTokenSource Stopping { get; } = new();

        public Task Receiving { get; set; } = Task.CompletedTask;

        public Task Announcing { get; set; } = Task.CompletedTask;

        public HashSet<IPAddress> ReportedConflicts { get; } = [];

        private long _lastMulticastTicks = DateTimeOffset.MinValue.UtcTicks;

        /// <summary>When the A record was last multicast; written by announcements and replies, read by the receive loop.</summary>
        public DateTimeOffset LastMulticast
        {
            get => new(Interlocked.Read(ref _lastMulticastTicks), TimeSpan.Zero);
            set => Interlocked.Exchange(ref _lastMulticastTicks, value.UtcTicks);
        }

        public void Dispose()
        {
            Stopping.Cancel();
            Stopping.Dispose();
            Transport.Dispose();
        }
    }
}
