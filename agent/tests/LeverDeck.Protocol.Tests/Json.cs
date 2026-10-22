using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using LeverDeck.Protocol.Serialization;

namespace LeverDeck.Protocol.Tests;

/// <summary>Serializes through the source-generated context, as the agent does.</summary>
internal static class Json
{
    /// <summary>The type a value is (de)serialized as: messages and actions go through their polymorphic root.</summary>
    public static Type RootOf(object value) => value switch
    {
        Message => typeof(Message),
        ActionDto => typeof(ActionDto),
        _ => value.GetType(),
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, TypeInfo(RootOf(value)));

    public static object? Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, TypeInfo(type));

    public static T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T))!;

    private static JsonTypeInfo TypeInfo(Type type) =>
        ProtocolJsonContext.Default.GetTypeInfo(type)
        ?? throw new InvalidOperationException($"{type} is not in the context.");
}
