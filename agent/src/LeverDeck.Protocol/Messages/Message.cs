using System.Text.Json.Serialization;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Protocol.Messages;

/// <summary>
/// One WebSocket message. The <c>kind</c> field (see <see cref="Serialization.MessageKinds"/>) selects the envelope;
/// commands, queries, query results and events add a <c>type</c> discriminator next to it.
/// System.Text.Json cannot nest two discriminators, so <see cref="Serialization.MessageConverter"/> handles
/// <c>kind</c> and each kind base below handles <c>type</c> with <see cref="JsonPolymorphicAttribute"/>.
/// </summary>
public abstract record Message
{
    private protected Message()
    {
    }
}

/// <summary>A command from a client; answered with an <see cref="AckMessage"/> or an <see cref="ErrorMessage"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConnectDeviceCommand), "connectDevice")]
[JsonDerivedType(typeof(ExecuteActionCommand), "executeAction")]
[JsonDerivedType(typeof(SaveProfileCommand), "saveProfile")]
[JsonDerivedType(typeof(DeleteProfileCommand), "deleteProfile")]
[JsonDerivedType(typeof(StartPairingCommand), "startPairing")]
[JsonDerivedType(typeof(CancelPairingCommand), "cancelPairing")]
[JsonDerivedType(typeof(RenameDeviceCommand), "renameDevice")]
[JsonDerivedType(typeof(RevokeDeviceCommand), "revokeDevice")]
[JsonDerivedType(typeof(UpdateSettingsCommand), "updateSettings")]
[JsonDerivedType(typeof(RegisterAppCommand), "registerApp")]
[JsonDerivedType(typeof(UnregisterAppCommand), "unregisterApp")]
[JsonDerivedType(typeof(SaveIconCommand), "saveIcon")]
[JsonDerivedType(typeof(DeleteIconCommand), "deleteIcon")]
public abstract record CommandMessage([property: JsonPropertyOrder(-1)] string RequestId) : Message;

/// <summary>A query from a client; answered with a <see cref="QueryResultMessage"/> or an <see cref="ErrorMessage"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GetProfilesQuery), "getProfiles")]
[JsonDerivedType(typeof(GetProfileQuery), "getProfile")]
[JsonDerivedType(typeof(GetDevicesQuery), "getDevices")]
[JsonDerivedType(typeof(GetPairingSessionQuery), "getPairingSession")]
[JsonDerivedType(typeof(GetSettingsQuery), "getSettings")]
[JsonDerivedType(typeof(GetAgentStatusQuery), "getAgentStatus")]
[JsonDerivedType(typeof(GetInstalledGamesQuery), "getInstalledGames")]
[JsonDerivedType(typeof(GetStoreAppsQuery), "getStoreApps")]
[JsonDerivedType(typeof(GetIconsQuery), "getIcons")]
[JsonDerivedType(typeof(GetObsScenesQuery), "getObsScenes")]
public abstract record QueryMessage([property: JsonPropertyOrder(-1)] string RequestId) : Message;

/// <summary>The answer to a query, correlated by <see cref="RequestId"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProfilesResult), "profiles")]
[JsonDerivedType(typeof(ProfileResult), "profile")]
[JsonDerivedType(typeof(DevicesResult), "devices")]
[JsonDerivedType(typeof(PairingSessionResult), "pairingSession")]
[JsonDerivedType(typeof(SettingsResult), "settings")]
[JsonDerivedType(typeof(AgentStatusResult), "agentStatus")]
[JsonDerivedType(typeof(InstalledGamesResult), "installedGames")]
[JsonDerivedType(typeof(StoreAppsResult), "storeApps")]
[JsonDerivedType(typeof(IconsResult), "icons")]
[JsonDerivedType(typeof(ObsScenesResult), "obsScenes")]
public abstract record QueryResultMessage([property: JsonPropertyOrder(-1)] string RequestId) : Message;

/// <summary>Pushed by the agent to keep clients in sync; it answers no request, so it has no request id.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProfileUpdatedEvent), "profileUpdated")]
[JsonDerivedType(typeof(ProfileDeletedEvent), "profileDeleted")]
[JsonDerivedType(typeof(DevicesChangedEvent), "devicesChanged")]
[JsonDerivedType(typeof(DeviceRevokedEvent), "deviceRevoked")]
[JsonDerivedType(typeof(PairingSessionChangedEvent), "pairingSessionChanged")]
[JsonDerivedType(typeof(SettingsUpdatedEvent), "settingsUpdated")]
[JsonDerivedType(typeof(ObsSceneChangedEvent), "obsSceneChanged")]
[JsonDerivedType(typeof(ObsStateChangedEvent), "obsStateChanged")]
[JsonDerivedType(typeof(IconsChangedEvent), "iconsChanged")]
public abstract record EventMessage : Message;

/// <summary>A command succeeded. Only <c>registerApp</c> carries a payload: the id of the new app.</summary>
public sealed record AckMessage(
    string RequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AppRegisteredPayload? Payload = null) : Message;

/// <summary>A command or query failed. <see cref="RequestId"/> is null when the request could not be read.</summary>
public sealed record ErrorMessage(string? RequestId, ErrorCodeDto Code, string Message) : Message;
