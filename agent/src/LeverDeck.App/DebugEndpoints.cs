#if DEBUG
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeverDeck.Application.Actions;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Profiles;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LeverDeck.App;

/// <summary>Body of <c>POST /debug/execute</c>: the stored button to run.</summary>
internal sealed record DebugExecuteRequest(Guid ProfileId, int PageIndex, Guid ButtonId);

internal sealed record DebugExecuteResponse(bool Ok, string? Code, string? Message);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DebugExecuteRequest))]
[JsonSerializable(typeof(DebugExecuteResponse))]
internal sealed partial class DebugJsonContext : JsonSerializerContext;

/// <summary>
/// Manual-check endpoint for Phase 4: runs a stored button through <see cref="ExecuteAction"/> as the admin. It is
/// compiled only into Debug builds, mapped only in the Development environment and answers only loopback callers;
/// everywhere else the route does not exist.
/// </summary>
internal static class DebugEndpoints
{
    public const string ExecutePath = "/debug/execute";

    public static IServiceCollection AddDebugEndpointServices(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, DebugJsonContext.Default));

    public static void MapDebugEndpoints(this WebApplication app) => app.MapPost(ExecutePath, ExecuteAsync);

    private static async Task<Results<Ok<DebugExecuteResponse>, BadRequest<DebugExecuteResponse>, NotFound>> ExecuteAsync(
        DebugExecuteRequest request,
        HttpContext context,
        ExecuteAction executeAction,
        CancellationToken cancellationToken)
    {
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
        {
            return TypedResults.NotFound();
        }

        if (request.ProfileId == Guid.Empty || request.ButtonId == Guid.Empty)
        {
            return TypedResults.BadRequest(new DebugExecuteResponse(false, "validationFailed", "Ids must not be empty."));
        }

        var result = await executeAction.ExecuteAsync(
            Caller.Admin,
            new ProfileId(request.ProfileId),
            request.PageIndex,
            new ButtonId(request.ButtonId),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? TypedResults.Ok(new DebugExecuteResponse(true, null, null))
            : TypedResults.BadRequest(new DebugExecuteResponse(false, result.Error!.Code.ToString(), result.Error.Message));
    }
}
#endif
