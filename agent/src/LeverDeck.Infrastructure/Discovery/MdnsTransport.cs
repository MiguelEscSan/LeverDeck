using System.Net;
using System.Net.Sockets;

namespace LeverDeck.Infrastructure.Discovery;

internal readonly record struct MdnsDatagram(int Length, IPEndPoint Source);

/// <summary>Sends and receives mDNS datagrams on one interface. Tests replace it with a fake.</summary>
internal interface IMdnsTransport : IDisposable
{
    ValueTask SendAsync(ReadOnlyMemory<byte> packet, IPEndPoint destination, CancellationToken cancellationToken);

    ValueTask<MdnsDatagram> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}

/// <summary>
/// UDP 5353 shared with Windows' own mDNS listener (Dnscache) and other responders: the socket is bound with
/// <c>SO_REUSEADDR</c> and without <c>SO_EXCLUSIVEADDRUSE</c>, so every socket that joined 224.0.0.251 gets each
/// multicast datagram. Membership and the outgoing multicast interface are the LAN interface. Multicast loopback is
/// on: in Winsock it governs the receive path, so the queries Windows' resolver sends from this PC reach us.
/// </summary>
internal sealed class UdpMdnsTransport : IMdnsTransport
{
    // SIO_UDP_CONNRESET: stops an ICMP "port unreachable" for a unicast reply from failing the next receive.
    private const int SioUdpConnReset = unchecked((int)0x9800000C);

    private static readonly IPEndPoint AnySource = new(IPAddress.Any, 0);

    private readonly Socket _socket;

    private UdpMdnsTransport(Socket socket) => _socket = socket;

    public static UdpMdnsTransport Open(IPAddress interfaceAddress)
    {
        ArgumentNullException.ThrowIfNull(interfaceAddress);

        var socket = CreateSocket();
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, MdnsResponder.Port));
            socket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                new MulticastOption(MdnsResponder.MulticastAddress, interfaceAddress));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, interfaceAddress.GetAddressBytes());
            return new UdpMdnsTransport(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>A configured, unbound socket (bind and membership need a real interface, so tests stop here).</summary>
    internal static Socket CreateSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.ExclusiveAddressUse = false;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255); // RFC 6762 §11
            socket.Ttl = 255;
            if (OperatingSystem.IsWindows())
            {
                socket.IOControl(SioUdpConnReset, [0, 0, 0, 0], null);
            }

            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> packet, IPEndPoint destination, CancellationToken cancellationToken) =>
        await _socket.SendToAsync(packet, SocketFlags.None, destination, cancellationToken).ConfigureAwait(false);

    public async ValueTask<MdnsDatagram> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, AnySource, cancellationToken).ConfigureAwait(false);
        return new MdnsDatagram(result.ReceivedBytes, (IPEndPoint)result.RemoteEndPoint);
    }

    /// <summary>Closing the socket also leaves the multicast group.</summary>
    public void Dispose() => _socket.Dispose();
}
