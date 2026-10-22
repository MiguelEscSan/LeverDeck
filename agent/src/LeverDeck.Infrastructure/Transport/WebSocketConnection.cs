using System.Buffers;
using System.Net.WebSockets;
using LeverDeck.Application.Security;

namespace LeverDeck.Infrastructure.Transport;

internal enum FrameKind
{
    Text,
    Binary,
    Closed,
    TooBig,
}

/// <summary>One complete message read from a socket, or why there is none.</summary>
internal readonly record struct Frame(FrameKind Kind, ReadOnlyMemory<byte> Payload)
{
    public static Frame Closed { get; } = new(FrameKind.Closed, ReadOnlyMemory<byte>.Empty);

    public static Frame TooBig { get; } = new(FrameKind.TooBig, ReadOnlyMemory<byte>.Empty);
}

/// <summary>
/// A live WebSocket and who is on the other side. Only the session reads from it; anyone may send, serialized by a
/// per-connection lock (a WebSocket allows one send at a time). Closing sends the close frame once and gives the
/// client <see cref="CloseGrace"/> to answer before the socket is aborted.
/// </summary>
public sealed class WebSocketConnection : IDisposable
{
    internal static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(2);

    private const int ReadChunkBytes = 4 * 1024;

    private readonly WebSocket _socket;
    private readonly int _maxMessageBytes;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime;
    private int _closing;

    internal WebSocketConnection(WebSocket socket, int maxMessageBytes, CancellationToken aborted)
    {
        _socket = socket;
        _maxMessageBytes = maxMessageBytes;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(aborted);
    }

    /// <summary>Public until the device handshake succeeds; admin connections are bound when accepted.</summary>
    public Caller Caller { get; private set; } = Caller.Public;

    public bool IsClosing => Volatile.Read(ref _closing) == 1;

    /// <summary>Cancelled when the client disconnects or the close grace runs out.</summary>
    internal CancellationToken Lifetime => _lifetime.Token;

    internal void Bind(Caller caller) => Caller = caller;

    /// <summary>Sends one text message; does nothing once the connection is closing.</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> utf8Json)
    {
        await _sendLock.WaitAsync(Lifetime).ConfigureAwait(false);
        try
        {
            if (!IsClosing && _socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(utf8Json, WebSocketMessageType.Text, endOfMessage: true, Lifetime).ConfigureAwait(false);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Starts the close handshake once; later calls do nothing. Never throws.</summary>
    public async Task CloseAsync(WebSocketCloseStatus status, string? reason)
    {
        if (Interlocked.Exchange(ref _closing, 1) == 1)
        {
            return;
        }

        try
        {
            await _sendLock.WaitAsync(Lifetime).ConfigureAwait(false);
            try
            {
                if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await _socket.CloseOutputAsync(status, reason, Lifetime).ConfigureAwait(false);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            // The client is already gone.
        }
        finally
        {
            try
            {
                _lifetime.CancelAfter(CloseGrace);
            }
            catch (ObjectDisposedException)
            {
                // The session already ended.
            }
        }
    }

    /// <summary>Reads one whole message; one larger than the limit is reported as <see cref="FrameKind.TooBig"/>.</summary>
    internal async Task<Frame> ReceiveAsync()
    {
        var buffer = new ArrayBufferWriter<byte>(ReadChunkBytes);
        while (true)
        {
            var result = await _socket.ReceiveAsync(buffer.GetMemory(ReadChunkBytes), Lifetime).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return Frame.Closed;
            }

            buffer.Advance(result.Count);
            if (buffer.WrittenCount > _maxMessageBytes)
            {
                return Frame.TooBig;
            }

            if (result.EndOfMessage)
            {
                var kind = result.MessageType == WebSocketMessageType.Text ? FrameKind.Text : FrameKind.Binary;
                return new Frame(kind, buffer.WrittenMemory);
            }
        }
    }

    public void Dispose()
    {
        _lifetime.Dispose();
        _sendLock.Dispose();
    }
}
