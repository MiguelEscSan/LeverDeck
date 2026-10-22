namespace LeverDeck.Protocol.Messages;

/// <summary>Must be the first message on <c>/ws</c>; anything else first closes the socket.</summary>
public sealed record ConnectDeviceCommand(string RequestId, ConnectDevicePayload Payload) : CommandMessage(RequestId);

public sealed record ExecuteActionCommand(string RequestId, ExecuteActionPayload Payload) : CommandMessage(RequestId);

public sealed record SaveProfileCommand(string RequestId, ProfilePayload Payload) : CommandMessage(RequestId);

public sealed record DeleteProfileCommand(string RequestId, ProfileIdPayload Payload) : CommandMessage(RequestId);

public sealed record StartPairingCommand(string RequestId) : CommandMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record CancelPairingCommand(string RequestId) : CommandMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record RenameDeviceCommand(string RequestId, RenameDevicePayload Payload) : CommandMessage(RequestId);

public sealed record RevokeDeviceCommand(string RequestId, DeviceIdPayload Payload) : CommandMessage(RequestId);

public sealed record UpdateSettingsCommand(string RequestId, UpdateSettingsPayload Payload) : CommandMessage(RequestId);

public sealed record RegisterAppCommand(string RequestId, RegisterAppPayload Payload) : CommandMessage(RequestId);

public sealed record UnregisterAppCommand(string RequestId, AppIdPayload Payload) : CommandMessage(RequestId);
