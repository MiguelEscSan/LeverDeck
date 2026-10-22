using LeverDeck.Application.Status;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Maps domain errors to the wire error codes of docs/ROADMAP.md (Phase 3.3).</summary>
public static class ErrorMapper
{
    public const string InternalMessage = "Internal error.";

    public static ErrorCodeDto ToCode(DomainErrorCode code) => code switch
    {
        DomainErrorCode.ValidationFailed
            or DomainErrorCode.DuplicateProfileName
            or DomainErrorCode.LastProfileCannotBeDeleted
            or DomainErrorCode.PositionOccupied
            or DomainErrorCode.PositionOutOfGrid
            or DomainErrorCode.GridTooSmall => ErrorCodeDto.ValidationFailed,
        DomainErrorCode.ProfileNotFound
            or DomainErrorCode.DeviceNotFound
            or DomainErrorCode.ButtonNotFound => ErrorCodeDto.NotFound,
        DomainErrorCode.PairingCodeInvalid => ErrorCodeDto.PairingCodeInvalid,
        DomainErrorCode.PairingCodeExpired => ErrorCodeDto.PairingCodeExpired,
        DomainErrorCode.DeviceRevoked => ErrorCodeDto.DeviceRevoked,
        DomainErrorCode.ActionNotExecutable or DomainErrorCode.ActionFailed => ErrorCodeDto.ActionFailed,
        DomainErrorCode.Unauthorized => ErrorCodeDto.Unauthorized,
        DomainErrorCode.Forbidden => ErrorCodeDto.Forbidden,
        DomainErrorCode.ProtocolMismatch => ErrorCodeDto.ProtocolMismatch,
        _ => ErrorCodeDto.Internal,
    };

    public static ErrorDto ToDto(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ErrorDto(ToCode(error.Code), error.Message);
    }

    public static ErrorMessage ToMessage(string? requestId, DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ErrorMessage(requestId, ToCode(error.Code), error.Message);
    }

    /// <summary>For unexpected failures: the details stay in the agent's log, never on the wire.</summary>
    public static ErrorMessage Internal(string? requestId) => new(requestId, ErrorCodeDto.Internal, InternalMessage);
}

/// <summary>Turns use-case results into the response envelope for a request.</summary>
public static class ResponseMapper
{
    /// <summary>A command result: an ack on success, an error otherwise.</summary>
    public static Message ToResponse(string requestId, Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? new AckMessage(requestId) : ErrorMapper.ToMessage(requestId, result.Error!);
    }

    /// <summary>A query (or value-returning command) result: <paramref name="onSuccess"/> builds the success message.</summary>
    public static Message ToResponse<T>(string requestId, Result<T> result, Func<T, Message> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : ErrorMapper.ToMessage(requestId, result.Error!);
    }
}

/// <summary>Maps the agent status; the OBS connection state comes from the OBS adapter (Phase 7).</summary>
public static class StatusMapper
{
    public static AgentStatusPayload ToPayload(AgentStatus status, bool obsConnected)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new AgentStatusPayload(status.AgentVersion, status.ProtocolVersion, obsConnected);
    }
}
