namespace LeverDeck.Domain.Errors;

/// <summary>Stable codes for every domain error, shared by thrown invariants and returned results.</summary>
public enum DomainErrorCode
{
    ValidationFailed,
    ProfileNotFound,
    DuplicateProfileName,
    LastProfileCannotBeDeleted,
    PositionOccupied,
    PositionOutOfGrid,
    GridTooSmall,
    PairingCodeInvalid,
    PairingCodeExpired,
    DeviceNotFound,
    DeviceRevoked,
    ButtonNotFound,
    ActionNotExecutable,
    Unauthorized,
    Forbidden,
    ProtocolMismatch,
}
