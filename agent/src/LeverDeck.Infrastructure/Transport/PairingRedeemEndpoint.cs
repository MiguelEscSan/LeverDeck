using System.Text.Json;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// <c>POST /api/pairing/redeem</c>: the only call a device makes before it has a token. Public scope; the pairing
/// session itself limits guesses. JSON only (a cross-site form post cannot send it without a CORS preflight, which the
/// agent never grants), small bodies only, and the response is never cached: it carries the device token, which is
/// never logged.
/// </summary>
public sealed partial class PairingRedeemEndpoint(PairDevice pairDevice, ILogger<PairingRedeemEndpoint> logger)
{
    public const string Path = "/api/pairing/redeem";

    /// <summary>A code and a 40-character name fit many times over.</summary>
    public const int MaxBodyBytes = 4 * 1024;

    public async Task HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        var response = context.Response;
        response.Headers.CacheControl = "no-store";

        if (!request.HasJsonContentType())
        {
            response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            await WriteErrorAsync(context, StatusCodes.Status413PayloadTooLarge, ErrorCodeDto.ValidationFailed, "The request body is too large.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxBodyBytes;
        }

        PairDeviceRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync(request.Body, ProtocolJsonContext.Default.PairDeviceRequest, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            body = null;
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteErrorAsync(context, StatusCodes.Status413PayloadTooLarge, ErrorCodeDto.ValidationFailed, "The request body is too large.")
                .ConfigureAwait(false);
            return;
        }

        if (body is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodeDto.ValidationFailed, "Expected { code, deviceName }.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await pairDevice.ExecuteAsync(Caller.Public, body.Code, body.DeviceName, context.RequestAborted).ConfigureAwait(false);
            if (result.IsFailure)
            {
                var error = ErrorMapper.ToDto(result.Error!);
                await WriteErrorAsync(context, StatusFor(result.Error!.Code), error.Code, error.Message).ConfigureAwait(false);
                return;
            }

            LogPaired(result.Value.DeviceId.Value);
            response.StatusCode = StatusCodes.Status200OK;
            await response.WriteAsJsonAsync(
                new PairDeviceResponse(result.Value.DeviceId.Value, result.Value.RawToken),
                ProtocolJsonContext.Default.PairDeviceResponse,
                contentType: null,
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRedeemFailed(exception);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, ErrorCodeDto.Internal, ErrorMapper.InternalMessage)
                .ConfigureAwait(false);
        }
    }

    private static int StatusFor(DomainErrorCode code) => ErrorMapper.ToCode(code) switch
    {
        ErrorCodeDto.ValidationFailed => StatusCodes.Status422UnprocessableEntity,
        ErrorCodeDto.Internal => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest,
    };

    private static Task WriteErrorAsync(HttpContext context, int status, ErrorCodeDto code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            new ErrorDto(code, message),
            ProtocolJsonContext.Default.ErrorDto,
            contentType: null,
            context.RequestAborted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Device {DeviceId} paired.")]
    private partial void LogPaired(Guid deviceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Redeeming a pairing code failed unexpectedly.")]
    private partial void LogRedeemFailed(Exception exception);
}
