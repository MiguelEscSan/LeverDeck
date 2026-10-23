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

/// <summary>Uploads an icon: a new <c>custom-</c> id chosen by the editor and the PNG image, base64-encoded.</summary>
public sealed record SaveIconCommand(string RequestId, SaveIconPayload Payload) : CommandMessage(RequestId);

public sealed record DeleteIconCommand(string RequestId, IconIdPayload Payload) : CommandMessage(RequestId);

/// <summary>Renames an uploaded sound clip. Uploads are not commands: they go over HTTP (<c>PUT /api/sounds/&lt;id&gt;.wav</c>).</summary>
public sealed record RenameSoundCommand(string RequestId, RenameSoundPayload Payload) : CommandMessage(RequestId);

public sealed record DeleteSoundCommand(string RequestId, SoundIdPayload Payload) : CommandMessage(RequestId);
