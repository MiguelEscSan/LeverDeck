namespace LeverDeck.Protocol.Messages;

public sealed record ProfileUpdatedEvent(ProfilePayload Payload) : EventMessage;

public sealed record ProfileDeletedEvent(ProfileIdPayload Payload) : EventMessage;

/// <summary>Admin connections only.</summary>
public sealed record DevicesChangedEvent(DevicesPayload Payload) : EventMessage;

/// <summary>Sent to the revoked device, then its socket closes.</summary>
public sealed record DeviceRevokedEvent(DeviceIdPayload Payload) : EventMessage;

/// <summary>Admin connections only.</summary>
public sealed record PairingSessionChangedEvent(PairingSessionPayload Payload) : EventMessage;

/// <summary>Admin connections only.</summary>
public sealed record SettingsUpdatedEvent(SettingsPayload Payload) : EventMessage;

/// <summary>Phase 7.</summary>
public sealed record ObsSceneChangedEvent(ObsSceneChangedPayload Payload) : EventMessage;

public sealed record IconsChangedEvent(IconsPayload Payload) : EventMessage;
