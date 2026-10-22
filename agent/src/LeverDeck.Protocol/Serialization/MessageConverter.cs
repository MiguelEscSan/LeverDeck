using System.Text.Json;
using System.Text.Json.Serialization;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Protocol.Serialization;

/// <summary>
/// Reads and writes the envelope's <c>kind</c> field, then hands the object to the kind's own contract, whose
/// <see cref="JsonPolymorphicAttribute"/> handles <c>type</c>. Every malformed message fails with a
/// <see cref="JsonException"/>. Metadata comes from the options' resolver, so it stays Native AOT safe.
/// </summary>
public sealed class MessageConverter : JsonConverter<Message>
{
    private const string KindProperty = "kind";
    private const string TypeProperty = "type";

    public override Message Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A message must be a JSON object.");
        }

        if (!root.TryGetProperty(KindProperty, out var kindElement) || kindElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A message needs a string 'kind'.");
        }

        var kind = kindElement.GetString();
        var entry = MessageKinds.All.FirstOrDefault(candidate => candidate.Kind == kind);
        if (entry.Type is null)
        {
            throw new JsonException($"Unknown message kind '{kind}'.");
        }

        if (entry.Type.IsAbstract
            && (!root.TryGetProperty(TypeProperty, out var typeElement) || typeElement.ValueKind != JsonValueKind.String))
        {
            throw new JsonException($"A '{kind}' message needs a string 'type'.");
        }

        try
        {
            return root.Deserialize(options.GetTypeInfo(entry.Type)) as Message
                ?? throw new JsonException($"A '{kind}' message must not be null.");
        }
        catch (NotSupportedException exception)
        {
            throw new JsonException($"Unsupported '{kind}' message.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var entry = MessageKinds.All.FirstOrDefault(candidate => candidate.Type.IsInstanceOfType(value));
        if (entry.Type is null)
        {
            throw new JsonException($"{value.GetType().Name} has no message kind.");
        }

        var body = JsonSerializer.SerializeToElement(value, options.GetTypeInfo(entry.Type));

        writer.WriteStartObject();
        writer.WriteString(KindProperty, entry.Kind);
        foreach (var property in body.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
