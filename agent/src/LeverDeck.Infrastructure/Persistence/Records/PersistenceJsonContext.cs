using System.Text.Json.Serialization;

namespace LeverDeck.Infrastructure.Persistence.Records;

/// <summary>
/// Source-generated metadata for the agent's files (Native AOT needs it). Indented camelCase JSON with enums by name, so
/// the files stay readable; strict reads: missing members and nulls in non-nullable members fail.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ProfilesDocument))]
[JsonSerializable(typeof(DevicesDocument))]
[JsonSerializable(typeof(SettingsDocument))]
internal sealed partial class PersistenceJsonContext : JsonSerializerContext;
