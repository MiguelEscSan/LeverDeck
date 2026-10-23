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

    /// <summary>An action executor tried to run the action and failed (for example, the OS rejected the input).</summary>
    ActionFailed,

    /// <summary>An uploaded icon that does not exist.</summary>
    IconNotFound,

    /// <summary>An uploaded sound clip that does not exist.</summary>
    SoundNotFound,
}
