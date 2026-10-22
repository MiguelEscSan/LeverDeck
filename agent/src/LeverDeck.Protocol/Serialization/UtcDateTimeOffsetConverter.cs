using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeverDeck.Protocol.Serialization;

/// <summary>
/// Writes instants as ISO 8601 UTC with milliseconds (<c>2026-10-22T08:30:00.125Z</c>) and reads any ISO 8601
/// instant, normalized to UTC.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
