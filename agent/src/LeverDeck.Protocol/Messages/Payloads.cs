using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Protocol.Messages;

/// <summary>The <c>{}</c> payload of messages that carry no data.</summary>
public sealed record EmptyPayload
{
    public static EmptyPayload Instance { get; } = new();
}

public sealed record ConnectDevicePayload(string Token, int ProtocolVersion)
{
    /// <summary>Redacted so the token never ends up in logs.</summary>
    public override string ToString() => $"ConnectDevicePayload {{ Token = ***, ProtocolVersion = {ProtocolVersion} }}";
}

public sealed record ExecuteActionPayload(Guid ProfileId, int PageIndex, Guid ButtonId);

public sealed record ProfileIdPayload(Guid ProfileId);

public sealed record ProfilePayload(ProfileDto Profile);

public sealed record ProfilesPayload(IReadOnlyList<ProfileSummaryDto> Profiles);

public sealed record DeviceIdPayload(Guid DeviceId);

public sealed record RenameDevicePayload(Guid DeviceId, string Name);

public sealed record DevicesPayload(IReadOnlyList<DeviceDto> Devices);

public sealed record PairingSessionPayload(PairingSessionDto? Session);

public sealed record UpdateSettingsPayload(SettingsUpdateDto Settings);

public sealed record SettingsPayload(SettingsDto Settings);

public sealed record RegisterAppPayload(string DisplayName, string Path);

public sealed record AppIdPayload(Guid AppId);

public sealed record AppRegisteredPayload(Guid AppId);

public sealed record AgentStatusPayload(string AgentVersion, int ProtocolVersion, bool ObsConnected);

public sealed record ObsSceneChangedPayload(string SceneName);
