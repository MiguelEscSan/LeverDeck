using System.Text.Json;
using System.Text.Json.Serialization;
using LeverDeck.Protocol.Messages;
using LeverDeck.Protocol.Serialization;

namespace LeverDeck.Protocol.Tests;

/// <summary>Keeps the samples, the serializer context and the hierarchies in sync with the contract types.</summary>
public sealed class CoverageTests
{
    private static readonly Type[] _contractTypes =
    [
        .. typeof(Message).Assembly.GetExportedTypes()
            .Where(type => type.Namespace is "LeverDeck.Protocol.Dtos" or "LeverDeck.Protocol.Messages")
            .OrderBy(type => type.FullName, StringComparer.Ordinal),
    ];

    [Fact]
    public void Samples_CoverEveryConcreteContractType()
    {
        var sampled = Samples.All.Select(sample => sample.GetType()).ToHashSet();

        var missing = _contractTypes
            .Where(type => type is { IsClass: true, IsAbstract: false } && !sampled.Contains(type))
            .ToArray();

        Assert.True(missing.Length == 0, $"Add samples for: {string.Join(", ", missing.Select(type => type.Name))}");
    }

    [Fact]
    public void Context_HasMetadataForEveryContractType()
    {
        var missing = _contractTypes.Where(type => ProtocolJsonContext.Default.GetTypeInfo(type) is null).ToArray();

        Assert.True(missing.Length == 0, $"Add to ProtocolJsonContext: {string.Join(", ", missing.Select(type => type.Name))}");
    }

    [Fact]
    public void ContractClasses_AreSealedOrAbstract()
    {
        var open = _contractTypes.Where(type => type is { IsClass: true, IsSealed: false, IsAbstract: false }).ToArray();

        Assert.True(open.Length == 0, $"Seal: {string.Join(", ", open.Select(type => type.Name))}");
    }

    [Fact]
    public void EveryEnumValue_IsWrittenAsCamelCaseString()
    {
        foreach (var enumType in _contractTypes.Where(type => type.IsEnum))
        {
            var typeInfo = ProtocolJsonContext.Default.GetTypeInfo(enumType)!;
            foreach (var value in Enum.GetValues(enumType))
            {
                var json = JsonSerializer.Serialize(value, typeInfo);

                Assert.Equal($"\"{JsonNamingPolicy.CamelCase.ConvertName(value.ToString()!)}\"", json);
            }
        }
    }

    [Fact]
    public void EveryConcreteMessage_BelongsToExactlyOneKind()
    {
        var concrete = _contractTypes.Where(type => typeof(Message).IsAssignableFrom(type) && !type.IsAbstract);

        foreach (var type in concrete)
        {
            Assert.Single(MessageKinds.All, entry => entry.Type.IsAssignableFrom(type));
        }
    }

    [Fact]
    public void EveryMessageWithAType_IsRegisteredOnItsKindBase()
    {
        var typed = _contractTypes.Where(type =>
            typeof(Message).IsAssignableFrom(type) && !type.IsAbstract && type.BaseType != typeof(Message));

        foreach (var type in typed)
        {
            var registered = type.BaseType!
                .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
                .Cast<JsonDerivedTypeAttribute>()
                .Any(attribute => attribute.DerivedType == type);

            Assert.True(registered, $"{type.Name} is not a JsonDerivedType of {type.BaseType!.Name}.");
        }
    }
}
