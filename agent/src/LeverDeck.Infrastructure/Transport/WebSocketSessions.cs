using System.Net.WebSockets;
using System.Text.Json;
using LeverDeck.Application.Devices;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Devices;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Infrastructure.Security;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using LeverDeck.Protocol.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// Runs the two WebSocket channels (docs/ARCHITECTURE.md, Transport endpoints). <c>/ws</c> must authenticate with
/// <c>connectDevice</c> as its first message within the connect timeout and is then bound to that device;
/// <c>/admin/ws</c> is checked against the admin token before the upgrade and bound to the admin. After that both
/// read one message at a time and answer it through the <see cref="MessageRouter"/>.
/// </summary>
public sealed partial class WebSocketSessions(
    ConnectDevice connectDevice,
    MessageRouter router,
    ConnectionRegistry registry,
    EventFanOut fanOut,
    AdminTokenStore adminTokens,
    LeverDeckOptions options,
    IHostApplicationLifetime lifetime,
    ILogger<WebSocketSessions> logger)
{
    /// <summary>A device sends only small commands and queries.</summary>
    public const int DeviceMaxMessageBytes = 64 * 1024;

    /// <summary>The editor sends whole profiles in <c>saveProfile</c>.</summary>
    public const int AdminMaxMessageBytes = 1024 * 1024;

    public const string AdminTokenQueryKey = "token";

    public async Task HandleDeviceAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var connection = new WebSocketConnection(socket, DeviceMaxMessageBytes, context.RequestAborted);
        using var stopping = CloseOnShutdown(connection);

        var receive = connection.ReceiveAsync();
        var deviceId = await HandshakeAsync(connection, receive).ConfigureAwait(false);
        if (deviceId is null)
        {
            await DrainAsync(connection, receive).ConfigureAwait(false);
            return;
        }

        connection.Bind(Caller.ForDevice(deviceId.Value));
        registry.Add(connection);
        LogDeviceConnected(deviceId.Value.Value);
        try
        {
            await PushDevicesQuietlyAsync().ConfigureAwait(false);
            await RunAsync(connection).ConfigureAwait(false);
        }
        finally
        {
            registry.Remove(connection);
            LogDeviceDisconnected(deviceId.Value.Value);
            await PushDevicesQuietlyAsync().ConfigureAwait(false);
        }
    }

    public async Task HandleAdminAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Checked before the upgrade: a connection without the right token never gets to send a message.
        if (!adminTokens.Verify(context.Request.Query[AdminTokenQueryKey].ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var connection = new WebSocketConnection(socket, AdminMaxMessageBytes, context.RequestAborted);
        using var stopping = CloseOnShutdown(connection);

        connection.Bind(Caller.Admin);
        registry.Add(connection);
        try
        {
            await RunAsync(connection).ConfigureAwait(false);
        }
        finally
        {
            registry.Remove(connection);
        }
    }

    private CancellationTokenRegistration CloseOnShutdown(WebSocketConnection connection) =>
        lifetime.ApplicationStopping.Register(() => _ = connection.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, CloseReasons.Shutdown));

    /// <summary>Authenticates the first message; on any failure the reason is sent and the close is started.</summary>
    private async Task<DeviceId?> HandshakeAsync(WebSocketConnection connection, Task<Frame> receive)
    {
        var first = await Task.WhenAny(receive, Task.Delay(options.ConnectTimeout, connection.Lifetime)).ConfigureAwait(false);
        if (first != receive || receive.IsCanceled || receive.IsFaulted)
        {
            await RejectAsync(connection, null, ErrorCodeDto.Unauthorized, "connectDevice did not arrive in time.").ConfigureAwait(false);
            return null;
        }

        var frame = await receive.ConfigureAwait(false);
        switch (frame.Kind)
        {
            case FrameKind.Closed:
                await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, null).ConfigureAwait(false);
                return null;
            case FrameKind.TooBig:
                await connection.CloseAsync(WebSocketCloseStatus.MessageTooBig, CloseReasons.For(ErrorCodeDto.ValidationFailed)).ConfigureAwait(false);
                return null;
        }

        var message = frame.Kind == FrameKind.Text ? TryRead(frame.Payload) : null;
        if (message is not ConnectDeviceCommand connect)
        {
            var requestId = frame.Kind == FrameKind.Text ? TryReadRequestId(frame.Payload) : null;
            await RejectAsync(connection, requestId, ErrorCodeDto.Unauthorized, "The first message must be connectDevice.").ConfigureAwait(false);
            return null;
        }

        try
        {
            var result = await connectDevice
                .ExecuteAsync(Caller.Public, connect.Payload.Token, connect.Payload.ProtocolVersion, connection.Lifetime)
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                var error = ErrorMapper.ToMessage(connect.RequestId, result.Error!);
                await connection.SendAsync(Serialize(error)).ConfigureAwait(false);
                await connection.CloseAsync(WebSocketCloseStatus.PolicyViolation, CloseReasons.For(error.Code)).ConfigureAwait(false);
                return null;
            }

            await connection.SendAsync(Serialize(new AckMessage(connect.RequestId))).ConfigureAwait(false);
            return result.Value;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or WebSocketException))
        {
            LogHandshakeFailed(exception);
            await connection.SendAsync(Serialize(ErrorMapper.Internal(connect.RequestId))).ConfigureAwait(false);
            await connection.CloseAsync(WebSocketCloseStatus.InternalServerError, CloseReasons.For(ErrorCodeDto.Internal)).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, null).ConfigureAwait(false);
            return null;
        }
    }

    private static async Task RejectAsync(WebSocketConnection connection, string? requestId, ErrorCodeDto code, string message)
    {
        try
        {
            await connection.SendAsync(Serialize(new ErrorMessage(requestId, code, message))).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            // The client is gone; there is nobody to tell.
        }

        await connection.CloseAsync(WebSocketCloseStatus.PolicyViolation, CloseReasons.For(code)).ConfigureAwait(false);
    }

    /// <summary>Waits for the client's close frame (or the grace timeout), discarding anything else it sends.</summary>
    private static async Task DrainAsync(WebSocketConnection connection, Task<Frame> pending)
    {
        try
        {
            var frame = await pending.ConfigureAwait(false);
            while (frame.Kind != FrameKind.Closed)
            {
                frame = await connection.ReceiveAsync().ConfigureAwait(false);
            }

            await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, null).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            // Aborted after the grace period, or the client dropped the connection.
        }
    }

    /// <summary>Answers messages one at a time until the client closes, the connection is closed or it drops.</summary>
    private async Task RunAsync(WebSocketConnection connection)
    {
        try
        {
            while (true)
            {
                var frame = await connection.ReceiveAsync().ConfigureAwait(false);
                if (frame.Kind == FrameKind.Closed)
                {
                    await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, null).ConfigureAwait(false);
                    return;
                }

                if (connection.IsClosing)
                {
                    continue;
                }

                if (frame.Kind == FrameKind.TooBig)
                {
                    await connection.CloseAsync(WebSocketCloseStatus.MessageTooBig, CloseReasons.For(ErrorCodeDto.ValidationFailed)).ConfigureAwait(false);
                    continue;
                }

                var response = await AnswerAsync(connection, frame).ConfigureAwait(false);
                await connection.SendAsync(Serialize(response)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            // The client dropped the connection, or it was aborted after the close grace period.
        }
    }

    private async Task<Message> AnswerAsync(WebSocketConnection connection, Frame frame)
    {
        if (frame.Kind != FrameKind.Text)
        {
            return MessageRouter.Invalid(null, "Messages must be JSON text.");
        }

        return TryRead(frame.Payload) switch
        {
            null => MessageRouter.Invalid(TryReadRequestId(frame.Payload), "Malformed or unknown message."),
            { } request when request is CommandMessage or QueryMessage =>
                await router.HandleAsync(connection.Caller, request, connection.Lifetime).ConfigureAwait(false),
            _ => MessageRouter.Invalid(TryReadRequestId(frame.Payload), "Clients send only commands and queries."),
        };
    }

    private async Task PushDevicesQuietlyAsync()
    {
        try
        {
            await fanOut.PushDevicesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogDevicesPushFailed(exception);
        }
    }

    private static byte[] Serialize(Message message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJsonContext.Default.Message);

    private static Message? TryRead(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload.Span, ProtocolJsonContext.Default.Message);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Best effort, so an error about an unreadable request can still be correlated by the client.</summary>
    private static string? TryReadRequestId(ReadOnlyMemory<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("requestId", out var requestId)
                && requestId.ValueKind == JsonValueKind.String
                    ? requestId.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Device {DeviceId} connected.")]
    private partial void LogDeviceConnected(Guid deviceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Device {DeviceId} disconnected.")]
    private partial void LogDeviceDisconnected(Guid deviceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Authenticating a device connection failed unexpectedly.")]
    private partial void LogHandshakeFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pushing the device list after a connection change failed.")]
    private partial void LogDevicesPushFailed(Exception exception);
}
