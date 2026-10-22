using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeverDeck.Protocol.Serialization;

/// <summary>Writes enum values as camelCase strings and rejects integers on read.</summary>
public sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where TEnum : struct, Enum;
