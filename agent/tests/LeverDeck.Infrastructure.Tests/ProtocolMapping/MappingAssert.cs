using System.Text.Json;
using LeverDeck.Domain.Common;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Serialization;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

internal static class MappingAssert
{
    /// <summary>The result failed with a domain error that reaches the wire as <c>validationFailed</c>.</summary>
    public static void IsValidationFailure(Result result)
    {
        Assert.True(result.IsFailure, "Expected a failed result.");
        Assert.Equal(ErrorCodeDto.ValidationFailed, ErrorMapper.ToCode(result.Error!.Code));
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    public static T Succeeds<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>DTOs hold lists, so compare them by their wire JSON.</summary>
    public static void SameJson<T>(T expected, T actual) =>
        Assert.Equal(Json(expected), Json(actual));

    public static string Json<T>(T value) =>
        JsonSerializer.Serialize(value, ProtocolJsonContext.Default.GetTypeInfo(typeof(T))!);
}
