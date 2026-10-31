using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LeverDeck.Infrastructure.Obs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Tests.Obs;

/// <summary>
/// A local stand-in for OBS Studio: speaks the obs-websocket v5 frames on 127.0.0.1 and answers from a table. It records
/// every request it receives, so the tests can check what the client sent.
/// </summary>
internal sealed class FakeObsServer : IAsyncDisposable
{
    public const string Challenge = "challenge-1";
    public const string Salt = "salt-1";

    private readonly WebApplication _app;
    private readonly string? _password;
    private readonly ConcurrentQueue<(string Type, string Data)> _requests = new();
    private WebSocket? _socket;

    private FakeObsServer(WebApplication app, string? password)
    {
        _app = app;
        _password = password;
    }

    /// <summary>The raw response of each request type; a type missing here is refused.</summary>
    public Dictionary<string, (bool Accepted, string? Comment, string ResponseData)> Answers { get; } = new()
    {
        ["GetStreamStatus"] = (true, null, """{"outputActive":false}"""),
        ["GetRecordStatus"] = (true, null, """{"outputActive":true}"""),
        ["GetSceneList"] = (true, null, """{"currentProgramSceneName":"Gameplay","scenes":[{"sceneName":"Starting soon","sceneIndex":1},{"sceneName":"Gameplay","sceneIndex":0}]}"""),
        ["GetSceneItemId"] = (true, null, """{"sceneItemId":7}"""),
        ["GetSceneItemEnabled"] = (true, null, """{"sceneItemEnabled":true}"""),
        ["SetSceneItemEnabled"] = (true, null, "{}"),
        ["ToggleStream"] = (true, null, "{}"),
        ["StartStream"] = (false, "Already streaming.", "{}"),
        ["SetCurrentProgramScene"] = (true, null, "{}"),
    };

    public IReadOnlyCollection<(string Type, string Data)> Requests => _requests.ToArray();

    public int Port => new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    public static async Task<FakeObsServer> StartAsync(string? password)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var server = new FakeObsServer(app, password);
        app.UseWebSockets();
        app.Use(async (HttpContext context, RequestDelegate _) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await server.HandleAsync(await context.WebSockets.AcceptWebSocketAsync()).ConfigureAwait(false);
        });
        await app.StartAsync().ConfigureAwait(false);
        return server;
    }

    /// <summary>Pushes an event frame to the connected client, as OBS does when its state changes.</summary>
    public Task PushEventAsync(string eventType, string eventData)
    {
        var socket = _socket ?? throw new InvalidOperationException("No client is connected.");
        return SendAsync(socket, Json("{'op':5,'d':{'eventType':'" + eventType + "','eventIntent':4,'eventData':" + eventData + "}}"));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private async Task HandleAsync(WebSocket socket)
    {
        _socket = socket;
        var hello = _password is null
            ? Json("{'op':0,'d':{'obsWebSocketVersion':'5.5.0','rpcVersion':1}}")
            : Json("{'op':0,'d':{'obsWebSocketVersion':'5.5.0','rpcVersion':1,'authentication':{'challenge':'" + Challenge + "','salt':'" + Salt + "'}}}");
        await SendAsync(socket, hello).ConfigureAwait(false);

        using (var identify = await ReceiveAsync(socket).ConfigureAwait(false))
        {
            var sent = identify!.RootElement.GetProperty("d").TryGetProperty("authentication", out var auth) ? auth.GetString() : null;
            var expected = _password is null ? null : ObsProtocol.Authentication(_password, Salt, Challenge);
            if (sent != expected)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication failed.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
        }

        await SendAsync(socket, Json("{'op':2,'d':{'negotiatedRpcVersion':1}}")).ConfigureAwait(false);

        while (socket.State == WebSocketState.Open)
        {
            using var message = await ReceiveAsync(socket).ConfigureAwait(false);
            if (message is null)
            {
                break;
            }

            var data = message.RootElement.GetProperty("d");
            var type = data.GetProperty("requestType").GetString()!;
            var id = data.GetProperty("requestId").GetString()!;
            var requestData = data.TryGetProperty("requestData", out var body) ? body.GetRawText() : string.Empty;
            _requests.Enqueue((type, requestData));

            var (accepted, comment, response) = Answers.TryGetValue(type, out var answer)
                ? answer
                : (false, "Unknown request.", "{}");
            var commentJson = comment is null ? string.Empty : $",'comment':'{comment}'";
            var status = Json($"{{'result':{(accepted ? "true" : "false")},'code':{(accepted ? 100 : 600)}{commentJson}}}");
            var frame = Json("{'op':7,'d':{'requestType':'" + type + "','requestId':'" + id + "','requestStatus':" + status + ",'responseData':" + response + "}}");
            await SendAsync(socket, frame).ConfigureAwait(false);
        }
    }

    /// <summary>Single quotes keep the frame templates readable; every frame is sent with double quotes.</summary>
    private static string Json(string template) => template.Replace('\'', '"');

    private static async Task SendAsync(WebSocket socket, string json) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

    private static async Task<JsonDocument?> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return JsonDocument.Parse(stream.ToArray());
            }
        }
    }
}
