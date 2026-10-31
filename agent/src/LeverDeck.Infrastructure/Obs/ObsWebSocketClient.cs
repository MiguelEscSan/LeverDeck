using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Obs;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Obs;

/// <summary>
/// The OBS Studio client: one WebSocket session at a time, opened with the stored host, port and password. Requests
/// are matched to their answers by id; OBS events become application events. Commands never connect on their own:
/// <see cref="ObsConnectionWorker"/> does, so a command on a dead link fails at once.
/// </summary>
public sealed partial class ObsWebSocketClient(
    ISettingsRepository settings,
    IEventPublisher events,
    ILogger<ObsWebSocketClient> logger) : IObsClient, IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private volatile Session? _session;

    public bool IsConnected => _session is { } session && session.Socket.State == WebSocketState.Open;

    public Task<Result<ObsSceneList>> GetScenesAsync(CancellationToken cancellationToken) =>
        RequestAsync("GetSceneList", null, ReadScenes, cancellationToken);


    public Task<Result> SetSceneAsync(string sceneName, CancellationToken cancellationToken) =>
        AckAsync("SetCurrentProgramScene", writer => writer.WriteString("sceneName", sceneName), cancellationToken);

    public Task<Result> ControlStreamAsync(ObsOutputCommand command, CancellationToken cancellationToken) =>
        AckAsync(
            command switch
            {
                ObsOutputCommand.Start => "StartStream",
                ObsOutputCommand.Stop => "StopStream",
                _ => "ToggleStream",
            },
            null,
            cancellationToken);

    public Task<Result> ControlRecordingAsync(ObsOutputCommand command, CancellationToken cancellationToken) =>
        AckAsync(
            command switch
            {
                ObsOutputCommand.Start => "StartRecord",
                ObsOutputCommand.Stop => "StopRecord",
                _ => "ToggleRecord",
            },
            null,
            cancellationToken);

    public Task<Result> ControlInputMuteAsync(string inputName, ObsMuteCommand command, CancellationToken cancellationToken) =>
        command == ObsMuteCommand.Toggle
            ? AckAsync("ToggleInputMute", writer => writer.WriteString("inputName", inputName), cancellationToken)
            : AckAsync(
                "SetInputMute",
                writer =>
                {
                    writer.WriteString("inputName", inputName);
                    writer.WriteBoolean("inputMuted", command == ObsMuteCommand.Mute);
                },
                cancellationToken);

    public async Task<Result> ControlSourceVisibilityAsync(
        string sceneName,
        string sourceName,
        ObsVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        var item = await RequestAsync(
            "GetSceneItemId",
            writer =>
            {
                writer.WriteString("sceneName", sceneName);
                writer.WriteString("sourceName", sourceName);
            },
            data => data.GetProperty("sceneItemId").GetInt64(),
            cancellationToken).ConfigureAwait(false);
        if (!item.IsSuccess)
        {
            return Result.Failure(item.Error!);
        }

        var itemId = item.Value;
        if (command != ObsVisibilityCommand.Toggle)
        {
            return await SetVisibilityAsync(sceneName, itemId, command == ObsVisibilityCommand.Show, cancellationToken).ConfigureAwait(false);
        }

        var current = await RequestAsync(
            "GetSceneItemEnabled",
            writer =>
            {
                writer.WriteString("sceneName", sceneName);
                writer.WriteNumber("sceneItemId", itemId);
            },
            data => data.GetProperty("sceneItemEnabled").GetBoolean(),
            cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess)
        {
            return Result.Failure(current.Error!);
        }

        return await SetVisibilityAsync(sceneName, itemId, !current.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a session when none is open and a connection is stored. When the stored connection changed, the open
    /// session is closed first. Called by <see cref="ObsConnectionWorker"/> only.
    /// </summary>
    public async Task MaintainAsync(CancellationToken cancellationToken)
    {
        var stored = (await settings.GetAsync(cancellationToken).ConfigureAwait(false)).ObsConnection;
        var current = _session;
        if (current is not null && current.Connection != stored)
        {
            await CloseAsync(current).ConfigureAwait(false);
        }

        if (stored is null || IsConnected)
        {
            return;
        }

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected)
            {
                await ConnectAsync(stored, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public void Dispose()
    {
        _session?.Stop.Cancel();
        _session?.Socket.Dispose();
        _connectGate.Dispose();
    }

    private async Task ConnectAsync(ObsConnection connection, CancellationToken cancellationToken)
    {
        var endpoint = $"{connection.Host}:{connection.Port}";
        var socket = new ClientWebSocket();
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshake.CancelAfter(ConnectTimeout);
            await socket.ConnectAsync(new UriBuilder("ws", connection.Host, connection.Port).Uri, handshake.Token).ConfigureAwait(false);

            using (var hello = await ReceiveAsync(socket, handshake.Token).ConfigureAwait(false))
            {
                if (hello is null || hello.RootElement.GetProperty("op").GetInt32() != ObsProtocol.OpHello)
                {
                    LogUnreachable(endpoint, "no hello");
                    socket.Dispose();
                    return;
                }

                var data = hello.RootElement.GetProperty("d");
                string? authentication = null;
                if (data.TryGetProperty("authentication", out var challenge))
                {
                    if (connection.Password is null)
                    {
                        LogUnreachable(endpoint, "password required");
                        socket.Dispose();
                        return;
                    }

                    authentication = ObsProtocol.Authentication(
                        connection.Password,
                        challenge.GetProperty("salt").GetString() ?? string.Empty,
                        challenge.GetProperty("challenge").GetString() ?? string.Empty);
                }

                await socket.SendAsync(ObsProtocol.Identify(authentication), WebSocketMessageType.Text, true, handshake.Token).ConfigureAwait(false);
            }

            using var identified = await ReceiveAsync(socket, handshake.Token).ConfigureAwait(false);
            if (identified is null || identified.RootElement.GetProperty("op").GetInt32() != ObsProtocol.OpIdentified)
            {
                LogUnreachable(endpoint, "identify refused");
                socket.Dispose();
                return;
            }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            socket.Dispose();
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogUnreachableError(endpoint, ex);
            return;
        }

        var session = new Session(socket, connection);
        _session = session;
        session.Receiver = Task.Run(() => ReceiveLoopAsync(session), CancellationToken.None);
        LogConnected(endpoint);
        await RefreshOutputStatesAsync(session).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(Session session)
    {
        try
        {
            while (session.Socket.State == WebSocketState.Open)
            {
                var message = await ReceiveAsync(session.Socket, session.Stop.Token).ConfigureAwait(false);
                if (message is null)
                {
                    break;
                }

                if (!await DispatchAsync(session, message).ConfigureAwait(false))
                {
                    message.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or OperationCanceledException or JsonException)
        {
            LogLinkLost(ex);
        }
        finally
        {
            Drop(session);
        }
    }

    /// <summary>Returns true when the document was handed to the request that waits for it, which then disposes it.</summary>
    private async Task<bool> DispatchAsync(Session session, JsonDocument message)
    {
        var root = message.RootElement;
        switch (root.GetProperty("op").GetInt32())
        {
            case ObsProtocol.OpRequestResponse:
                var requestId = root.GetProperty("d").GetProperty("requestId").GetString() ?? string.Empty;
                return session.Pending.TryRemove(requestId, out var waiter) && waiter.TrySetResult(message);

            case ObsProtocol.OpEvent:
                await HandleEventAsync(session, root.GetProperty("d")).ConfigureAwait(false);
                return false;

            default:
                return false;
        }
    }

    private async Task HandleEventAsync(Session session, JsonElement evt)
    {
        var eventType = evt.GetProperty("eventType").GetString();
        var data = evt.TryGetProperty("eventData", out var payload) ? payload : default;
        switch (eventType)
        {
            case "CurrentProgramSceneChanged":
                await events.PublishAsync(
                    new ObsSceneChanged(data.GetProperty("sceneName").GetString() ?? string.Empty),
                    session.Stop.Token).ConfigureAwait(false);
                break;

            case "StreamStateChanged":
                session.Streaming = data.GetProperty("outputActive").GetBoolean();
                await PublishStateAsync(session).ConfigureAwait(false);
                break;

            case "RecordStateChanged":
                session.Recording = data.GetProperty("outputActive").GetBoolean();
                await PublishStateAsync(session).ConfigureAwait(false);
                break;
        }
    }

    private async Task RefreshOutputStatesAsync(Session session)
    {
        var stream = await RequestAsync("GetStreamStatus", null, data => data.GetProperty("outputActive").GetBoolean(), session.Stop.Token).ConfigureAwait(false);
        var recording = await RequestAsync("GetRecordStatus", null, data => data.GetProperty("outputActive").GetBoolean(), session.Stop.Token).ConfigureAwait(false);
        session.Streaming = stream.IsSuccess && stream.Value;
        session.Recording = recording.IsSuccess && recording.Value;
        await PublishStateAsync(session).ConfigureAwait(false);
    }

    private Task PublishStateAsync(Session session) =>
        events.PublishAsync(new ObsStateChanged(session.Streaming, session.Recording), session.Stop.Token);

    private Task<Result> SetVisibilityAsync(string sceneName, long itemId, bool enabled, CancellationToken cancellationToken) =>
        AckAsync(
            "SetSceneItemEnabled",
            writer =>
            {
                writer.WriteString("sceneName", sceneName);
                writer.WriteNumber("sceneItemId", itemId);
                writer.WriteBoolean("sceneItemEnabled", enabled);
            },
            cancellationToken);

    private async Task<Result> AckAsync(string requestType, Action<Utf8JsonWriter>? writeData, CancellationToken cancellationToken)
    {
        var answer = await RequestAsync(requestType, writeData, _ => true, cancellationToken).ConfigureAwait(false);
        return answer.IsSuccess ? Result.Success() : Result.Failure(answer.Error!);
    }

    private async Task<Result<T>> RequestAsync<T>(
        string requestType,
        Action<Utf8JsonWriter>? writeData,
        Func<JsonElement, T> readData,
        CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null || session.Socket.State != WebSocketState.Open)
        {
            return Result.Failure<T>(Unavailable("OBS is not connected."));
        }

        var requestId = Guid.NewGuid().ToString("N");
        var answer = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Pending[requestId] = answer;
        try
        {
            await session.SendAsync(ObsProtocol.Request(requestId, requestType, writeData), cancellationToken).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            using var document = await answer.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            var data = document.RootElement.GetProperty("d");
            var status = data.GetProperty("requestStatus");
            if (!status.GetProperty("result").GetBoolean())
            {
                var comment = status.TryGetProperty("comment", out var text) ? text.GetString() : null;
                return Result.Failure<T>(new DomainError(
                    DomainErrorCode.ActionFailed,
                    comment ?? $"OBS refused {requestType} (code {status.GetProperty("code").GetInt32()})."));
            }

            var response = data.TryGetProperty("responseData", out var responseData) ? responseData : default;
            return Result.Success(readData(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<T>(Unavailable($"OBS did not answer {requestType} in time."));
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            return Result.Failure<T>(Unavailable("The OBS connection failed."));
        }
        finally
        {
            session.Pending.TryRemove(requestId, out _);
        }
    }

    private async Task CloseAsync(Session session)
    {
        if (ReferenceEquals(_session, session))
        {
            _session = null;
        }

        session.Stop.Cancel();
        if (session.Socket.State == WebSocketState.Open)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            timeout.CancelAfter(ConnectTimeout);
            await session.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token).ConfigureAwait(false);
        }

        session.Socket.Dispose();
    }

    private void Drop(Session session)
    {
        if (ReferenceEquals(_session, session))
        {
            _session = null;
        }

        foreach (var waiter in session.Pending.Values)
        {
            waiter.TrySetException(new InvalidOperationException("OBS disconnected."));
        }

        session.Pending.Clear();
        session.Socket.Dispose();
        session.Dispose();
        LogDisconnected();
    }

    private static ObsSceneList ReadScenes(JsonElement data)
    {
        var scenes = data.GetProperty("scenes").EnumerateArray()
            .Select(scene => (Index: scene.TryGetProperty("sceneIndex", out var index) ? index.GetInt32() : 0, Name: scene.GetProperty("sceneName").GetString() ?? string.Empty))
            .OrderBy(scene => scene.Index)
            .Select(scene => scene.Name)
            .ToList();
        var current = data.TryGetProperty("currentProgramSceneName", out var name) ? name.GetString() : null;
        return new ObsSceneList(scenes, current);
    }

    private static DomainError Unavailable(string message) => new(DomainErrorCode.ActionFailed, message);

    private static async Task<JsonDocument?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.GetMemory(4096), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            buffer.Advance(result.Count);
            if (result.EndOfMessage)
            {
                return JsonDocument.Parse(buffer.WrittenMemory);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to OBS at {Endpoint}.")]
    private partial void LogConnected(string endpoint);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not open the OBS connection to {Endpoint} ({Reason}).")]
    private partial void LogUnreachable(string endpoint, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not open the OBS connection to {Endpoint}.")]
    private partial void LogUnreachableError(string endpoint, Exception error);

    [LoggerMessage(Level = LogLevel.Information, Message = "The OBS connection ended.")]
    private partial void LogLinkLost(Exception error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disconnected from OBS.")]
    private partial void LogDisconnected();

    private sealed class Session(ClientWebSocket socket, ObsConnection connection) : IDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);

        public ClientWebSocket Socket { get; } = socket;

        public ObsConnection Connection { get; } = connection;

        public ConcurrentDictionary<string, TaskCompletionSource<JsonDocument>> Pending { get; } = new();

        public CancellationTokenSource Stop { get; } = new();

        public Task Receiver { get; set; } = Task.CompletedTask;

        public bool Streaming { get; set; }

        public bool Recording { get; set; }

        public async Task SendAsync(byte[] frame, CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Socket.SendAsync(frame, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public void Dispose() => _sendGate.Dispose();
    }
}
