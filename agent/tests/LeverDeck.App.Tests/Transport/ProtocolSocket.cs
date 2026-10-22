using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LeverDeck.Infrastructure.Security;
using LeverDeck.Protocol;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using LeverDeck.Protocol.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LeverDeck.App.Tests.Transport;

/// <summary>A test-side WebSocket that speaks the wire contract.</summary>
public sealed class ProtocolSocket(WebSocket socket) : IDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    public WebSocket Socket => socket;

    public Task SendAsync(Message message) =>
        SendTextAsync(JsonSerializer.Serialize(message, ProtocolJsonContext.Default.Message));

    public async Task SendTextAsync(string text)
    {
        using var timeout = new CancellationTokenSource(ReceiveTimeout);
        await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
    }

    /// <summary>The next message, or <see langword="null"/> once the agent closed the socket (the close is answered).</summary>
    public async Task<Message?> ReceiveAsync()
    {
        using var timeout = new CancellationTokenSource(ReceiveTimeout);
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
                }

                return null;
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return JsonSerializer.Deserialize(message.ToArray(), ProtocolJsonContext.Default.Message);
            }
        }
    }

    /// <summary>Skips other messages (events) until one of type <typeparamref name="T"/> arrives.</summary>
    public async Task<T> ReceiveAsync<T>(Func<T, bool>? match = null)
        where T : Message
    {
        while (true)
        {
            var message = await ReceiveAsync() ?? throw new InvalidOperationException(
                $"The socket closed ({socket.CloseStatus} {socket.CloseStatusDescription}) before a {typeof(T).Name} arrived.");
            if (message is T typed && (match is null || match(typed)))
            {
                return typed;
            }
        }
    }

    /// <summary>Skips messages until the agent closes the socket; returns the messages seen on the way.</summary>
    public async Task<IReadOnlyList<Message>> ReceiveUntilClosedAsync()
    {
        var seen = new List<Message>();
        while (await ReceiveAsync() is { } message)
        {
            seen.Add(message);
        }

        return seen;
    }

    /// <summary>Sends a request and returns the response with the same request id, skipping events.</summary>
    public async Task<Message> RequestAsync(Message request)
    {
        var requestId = request switch
        {
            CommandMessage command => command.RequestId,
            QueryMessage query => query.RequestId,
            _ => throw new ArgumentException("Only commands and queries are requests.", nameof(request)),
        };

        await SendAsync(request);
        return await ReceiveAsync<Message>(message => message switch
        {
            AckMessage ack => ack.RequestId == requestId,
            ErrorMessage error => error.RequestId == requestId,
            QueryResultMessage result => result.RequestId == requestId,
            _ => false,
        });
    }

    public void Dispose() => socket.Dispose();
}

/// <summary>Pairs and connects clients the way the PWA and the editor do.</summary>
public static class Clients
{
    public static string NewRequestId() => Guid.NewGuid().ToString("N");

    public static string AdminToken(TransportAppFactory factory) =>
        factory.Services.GetRequiredService<AdminTokenStore>().GetOrCreate();

    public static async Task<ProtocolSocket> ConnectAdminAsync(TransportAppFactory factory, string? token = null)
    {
        token ??= AdminToken(factory);
        var socket = await factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri($"ws://localhost/admin/ws?token={Uri.EscapeDataString(token)}"), CancellationToken.None);
        return new ProtocolSocket(socket);
    }

    public static async Task<ProtocolSocket> OpenDeviceSocketAsync(TransportAppFactory factory)
    {
        var socket = await factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/ws"), CancellationToken.None);
        return new ProtocolSocket(socket);
    }

    public static async Task<string> StartPairingAsync(ProtocolSocket admin)
    {
        Assert.IsType<AckMessage>(await admin.RequestAsync(new StartPairingCommand(NewRequestId())));
        var result = Assert.IsType<PairingSessionResult>(await admin.RequestAsync(new GetPairingSessionQuery(NewRequestId())));
        return Assert.IsType<PairingSessionDto>(result.Payload.Session).Code;
    }

    public static async Task<HttpResponseMessage> RedeemAsync(TransportAppFactory factory, string code, string deviceName)
    {
        using var client = factory.CreateClient();
        return await client.PostAsync(
            new Uri("/api/pairing/redeem", UriKind.Relative),
            JsonContent.Create(new PairDeviceRequest(code, deviceName), ProtocolJsonContext.Default.PairDeviceRequest));
    }

    public static async Task<PairDeviceResponse> PairAsync(TransportAppFactory factory, ProtocolSocket admin, string deviceName = "Phone")
    {
        var code = await StartPairingAsync(admin);
        using var response = await RedeemAsync(factory, code, deviceName);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.PairDeviceResponse))!;
    }

    public static async Task<ProtocolSocket> ConnectDeviceAsync(TransportAppFactory factory, string deviceToken)
    {
        var device = await OpenDeviceSocketAsync(factory);
        var response = await device.RequestAsync(
            new ConnectDeviceCommand(NewRequestId(), new ConnectDevicePayload(deviceToken, ProtocolInfo.ProtocolVersion)));
        Assert.IsType<AckMessage>(response);
        return device;
    }
}
