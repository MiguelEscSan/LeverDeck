using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LeverDeck.Infrastructure.Obs;

/// <summary>
/// The OBS WebSocket v5 wire format, as pure functions: the frames the agent sends and the authentication string. The
/// socket lives in <see cref="ObsWebSocketClient"/>. Built by hand with <see cref="Utf8JsonWriter"/> so it stays
/// Native AOT safe.
/// </summary>
internal static class ObsProtocol
{
    public const int RpcVersion = 1;

    public const int OpHello = 0;
    public const int OpIdentify = 1;
    public const int OpIdentified = 2;
    public const int OpEvent = 5;
    public const int OpRequest = 6;
    public const int OpRequestResponse = 7;

    // obs-websocket EventSubscription bits: Scenes (1 << 2) carries CurrentProgramSceneChanged; Outputs (1 << 6) carries
    // StreamStateChanged and RecordStateChanged. Everything else stays unsubscribed.
    public const int EventSubscriptions = (1 << 2) | (1 << 6);

    /// <summary>The <c>authentication</c> string of Identify: base64(sha256(base64(sha256(password + salt)) + challenge)).</summary>
    public static string Authentication(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public static byte[] Identify(string? authentication) => Frame(OpIdentify, writer =>
    {
        writer.WriteNumber("rpcVersion", RpcVersion);
        if (authentication is not null)
        {
            writer.WriteString("authentication", authentication);
        }

        writer.WriteNumber("eventSubscriptions", EventSubscriptions);
    });

    public static byte[] Request(string requestId, string requestType, Action<Utf8JsonWriter>? writeData = null) =>
        Frame(OpRequest, writer =>
        {
            writer.WriteString("requestType", requestType);
            writer.WriteString("requestId", requestId);
            if (writeData is not null)
            {
                writer.WriteStartObject("requestData");
                writeData(writer);
                writer.WriteEndObject();
            }
        });

    private static byte[] Frame(int op, Action<Utf8JsonWriter> writeData)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("op", op);
            writer.WriteStartObject("d");
            writeData(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
