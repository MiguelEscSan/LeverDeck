using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Errors;

public sealed class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithCodeAndMessage_ExposesBoth()
    {
        var exception = new InvariantViolationException(DomainErrorCode.GridTooSmall, "Too small.");

        Assert.Equal(DomainErrorCode.GridTooSmall, exception.Code);
        Assert.Equal("Too small.", exception.Message);
    }

    [Fact]
    public void ToError_ReturnsErrorWithSameCodeAndMessage()
    {
        DomainException exception = new InvariantViolationException(DomainErrorCode.PositionOccupied, "Taken.");

        var error = exception.ToError();

        Assert.Equal(new DomainError(DomainErrorCode.PositionOccupied, "Taken."), error);
    }

    [Fact]
    public void DomainErrorCode_ContainsEveryRoadmapCode()
    {
        string[] expected =
        [
            "ValidationFailed", "ProfileNotFound", "DuplicateProfileName", "LastProfileCannotBeDeleted",
            "PositionOccupied", "PositionOutOfGrid", "GridTooSmall", "PairingCodeInvalid", "PairingCodeExpired",
            "DeviceNotFound", "DeviceRevoked", "ButtonNotFound", "ActionNotExecutable", "Unauthorized", "Forbidden", "ProtocolMismatch",
        ];

        Assert.Equal(expected, Enum.GetNames<DomainErrorCode>());
    }
}
