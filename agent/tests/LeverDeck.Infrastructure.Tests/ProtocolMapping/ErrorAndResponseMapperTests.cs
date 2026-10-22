using LeverDeck.Application.Status;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class ErrorAndResponseMapperTests
{
    public static TheoryData<DomainErrorCode, ErrorCodeDto> ExpectedCodes() => new()
    {
        { DomainErrorCode.ValidationFailed, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.ProfileNotFound, ErrorCodeDto.NotFound },
        { DomainErrorCode.DuplicateProfileName, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.LastProfileCannotBeDeleted, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.PositionOccupied, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.PositionOutOfGrid, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.GridTooSmall, ErrorCodeDto.ValidationFailed },
        { DomainErrorCode.PairingCodeInvalid, ErrorCodeDto.PairingCodeInvalid },
        { DomainErrorCode.PairingCodeExpired, ErrorCodeDto.PairingCodeExpired },
        { DomainErrorCode.DeviceNotFound, ErrorCodeDto.NotFound },
        { DomainErrorCode.DeviceRevoked, ErrorCodeDto.DeviceRevoked },
        { DomainErrorCode.ButtonNotFound, ErrorCodeDto.NotFound },
        { DomainErrorCode.IconNotFound, ErrorCodeDto.NotFound },
        { DomainErrorCode.ActionNotExecutable, ErrorCodeDto.ActionFailed },
        { DomainErrorCode.Unauthorized, ErrorCodeDto.Unauthorized },
        { DomainErrorCode.Forbidden, ErrorCodeDto.Forbidden },
        { DomainErrorCode.ProtocolMismatch, ErrorCodeDto.ProtocolMismatch },
        { DomainErrorCode.ActionFailed, ErrorCodeDto.ActionFailed },
    };

    [Theory]
    [MemberData(nameof(ExpectedCodes))]
    public void DomainCode_MapsToItsWireCode(DomainErrorCode domain, ErrorCodeDto wire)
    {
        Assert.Equal(wire, ErrorMapper.ToCode(domain));
    }

    [Fact]
    public void EveryDomainCode_HasAnExpectedWireCode()
    {
        var covered = ExpectedCodes().Select(row => (DomainErrorCode)row[0]).ToHashSet();

        Assert.All(Enum.GetValues<DomainErrorCode>(), code => Assert.Contains(code, covered));
    }

    [Fact]
    public void UnknownDomainCode_MapsToInternal()
    {
        Assert.Equal(ErrorCodeDto.Internal, ErrorMapper.ToCode((DomainErrorCode)500));
    }

    [Fact]
    public void Error_KeepsTheMessage()
    {
        var error = new DomainError(DomainErrorCode.ProfileNotFound, "Profile x does not exist.");

        Assert.Equal(new ErrorDto(ErrorCodeDto.NotFound, "Profile x does not exist."), ErrorMapper.ToDto(error));
        Assert.Equal(new ErrorMessage("r1", ErrorCodeDto.NotFound, "Profile x does not exist."), ErrorMapper.ToMessage("r1", error));
    }

    [Fact]
    public void Internal_HidesDetails()
    {
        var message = ErrorMapper.Internal(null);

        Assert.Null(message.RequestId);
        Assert.Equal(ErrorCodeDto.Internal, message.Code);
        Assert.Equal("Internal error.", message.Message);
    }

    [Fact]
    public void SuccessfulCommand_BecomesAck()
    {
        Assert.Equal(new AckMessage("r1"), ResponseMapper.ToResponse("r1", Result.Success()));
    }

    [Fact]
    public void FailedCommand_BecomesError()
    {
        var failed = Result.Failure(new DomainError(DomainErrorCode.Forbidden, "Admin only."));

        Assert.Equal(new ErrorMessage("r1", ErrorCodeDto.Forbidden, "Admin only."), ResponseMapper.ToResponse("r1", failed));
    }

    [Fact]
    public void SuccessfulQuery_UsesTheResultFactory()
    {
        var status = new AgentStatus(1, "1.2.3");

        var response = ResponseMapper.ToResponse(
            "q1",
            Result.Success(status),
            value => new AgentStatusResult("q1", StatusMapper.ToPayload(value, obsConnected: false)));

        Assert.Equal(new AgentStatusResult("q1", new AgentStatusPayload("1.2.3", ProtocolInfo.ProtocolVersion, false)), response);
    }

    [Fact]
    public void FailedQuery_BecomesErrorWithoutCallingTheFactory()
    {
        var failed = Result.Failure<AgentStatus>(new DomainError(DomainErrorCode.Unauthorized, "Connect first."));

        var response = ResponseMapper.ToResponse<AgentStatus>("q1", failed, _ => throw new InvalidOperationException("Not called."));

        Assert.Equal(new ErrorMessage("q1", ErrorCodeDto.Unauthorized, "Connect first."), response);
    }

    [Fact]
    public void Ids_RejectTheEmptyGuid()
    {
        var id = Guid.Parse("66666666-6666-6666-6666-666666666666");

        Assert.Equal(id, MappingAssert.Succeeds(IdMapper.ToProfileId(id)).Value);
        Assert.Equal(id, MappingAssert.Succeeds(IdMapper.ToButtonId(id)).Value);
        Assert.Equal(id, MappingAssert.Succeeds(IdMapper.ToDeviceId(id)).Value);
        Assert.Equal(id, MappingAssert.Succeeds(IdMapper.ToAppId(id)).Value);
        MappingAssert.IsValidationFailure(IdMapper.ToProfileId(Guid.Empty));
        MappingAssert.IsValidationFailure(IdMapper.ToButtonId(Guid.Empty));
        MappingAssert.IsValidationFailure(IdMapper.ToDeviceId(Guid.Empty));
        MappingAssert.IsValidationFailure(IdMapper.ToAppId(Guid.Empty));
    }
}
