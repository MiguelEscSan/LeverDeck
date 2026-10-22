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

public sealed record InstalledGamesPayload(IReadOnlyList<InstalledGameDto> Games);

/// <summary><see cref="Data"/> is the PNG image in base64 (at most 256 KiB once decoded).</summary>
public sealed record SaveIconPayload(string IconId, string Data);

public sealed record IconIdPayload(string IconId);

/// <summary>The uploaded icons' ids; each is served as <c>/api/icons/&lt;id&gt;.png</c>.</summary>
public sealed record IconsPayload(IReadOnlyList<string> Icons);
