using System.Text.Json.Serialization;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Protocol.Serialization;

/// <summary>
/// Source-generated metadata for the whole wire contract (Native AOT needs it). camelCase names, strict reads:
/// missing constructor arguments and nulls in non-nullable members fail, and metadata may come in any order.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowOutOfOrderMetadataProperties = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters = [typeof(MessageConverter), typeof(UtcDateTimeOffsetConverter)])]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(CommandMessage))]
[JsonSerializable(typeof(QueryMessage))]
[JsonSerializable(typeof(QueryResultMessage))]
[JsonSerializable(typeof(EventMessage))]
[JsonSerializable(typeof(AckMessage))]
[JsonSerializable(typeof(ErrorMessage))]
[JsonSerializable(typeof(ActionDto))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(PairDeviceRequest))]
[JsonSerializable(typeof(PairDeviceResponse))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext;
