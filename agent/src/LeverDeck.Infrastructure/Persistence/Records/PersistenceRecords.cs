using System.Text.Json.Serialization;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Infrastructure.Persistence.Records;

// The on-disk shape of the agent's files. Deliberately separate from the wire DTOs: devices.json keeps token hashes,
// which never go on the wire, and each file can evolve on its own through SchemaVersion.

internal sealed record GridSizeRecord(int Columns, int Rows);

internal sealed record ProfilesDocument(int SchemaVersion, List<ProfileRecord> Profiles)
{
    public const int CurrentVersion = 1;
}

internal sealed record ProfileRecord(Guid Id, string Name, KeyColor Color, GridSizeRecord GridSize, List<PageRecord> Pages);

/// <summary>A page; its index is its position in <see cref="ProfileRecord.Pages"/>.</summary>
internal sealed record PageRecord(List<ButtonRecord> Buttons);

internal sealed record ButtonRecord(
    Guid Id,
    int Row,
    int Column,
    string Label,
    KeyColor Color,
    string Icon,
    ActionRecord Action);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HotkeyRecord), "hotkey")]
[JsonDerivedType(typeof(MediaKeyRecord), "mediaKey")]
[JsonDerivedType(typeof(LaunchAppRecord), "launchApp")]
[JsonDerivedType(typeof(OpenUrlRecord), "openUrl")]
[JsonDerivedType(typeof(ObsSceneRecord), "obsScene")]
[JsonDerivedType(typeof(ObsStreamingRecord), "obsStreaming")]
[JsonDerivedType(typeof(ObsRecordingRecord), "obsRecord")]
[JsonDerivedType(typeof(ObsInputMuteRecord), "obsInputMute")]
[JsonDerivedType(typeof(ObsSourceVisibilityRecord), "obsSourceVisibility")]
[JsonDerivedType(typeof(LaunchGameRecord), "launchGame")]
[JsonDerivedType(typeof(ChangePageRecord), "changePage")]
[JsonDerivedType(typeof(SwitchProfileRecord), "switchProfile")]
internal abstract record ActionRecord;

internal sealed record HotkeyRecord(KeyModifiers Modifiers, VirtualKey Key) : ActionRecord;

internal sealed record MediaKeyRecord(MediaKeyKind Key) : ActionRecord;

internal sealed record LaunchAppRecord(Guid AppId) : ActionRecord;

internal sealed record OpenUrlRecord(string Url) : ActionRecord;

internal sealed record ObsSceneRecord(string SceneName) : ActionRecord;

internal sealed record ObsStreamingRecord(ObsOutputCommand Command) : ActionRecord;

internal sealed record ObsRecordingRecord(ObsOutputCommand Command) : ActionRecord;

internal sealed record ObsInputMuteRecord(string InputName, ObsMuteCommand Command) : ActionRecord;

internal sealed record ObsSourceVisibilityRecord(string SceneName, string SourceName, ObsVisibilityCommand Command) : ActionRecord;

internal sealed record LaunchGameRecord(GameStore Store, string GameId) : ActionRecord;

internal sealed record ChangePageRecord(PageTargetKind Target, int? PageIndex) : ActionRecord;

internal sealed record SwitchProfileRecord(Guid ProfileId) : ActionRecord;

internal sealed record DevicesDocument(int SchemaVersion, List<DeviceRecord> Devices)
{
    public const int CurrentVersion = 1;
}

internal sealed record DeviceRecord(
    Guid Id,
    string Name,
    string TokenHash,
    DateTimeOffset PairedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? RevokedAt);

internal sealed record SettingsDocument(
    int SchemaVersion,
    bool StartWithWindows,
    GridSizeRecord DefaultGridSize,
    ObsConnectionRecord? Obs,
    List<RegisteredAppRecord> RegisteredApps)
{
    public const int CurrentVersion = 1;
}

/// <summary>The OBS password is stored DPAPI-protected (base64), never in plain text.</summary>
internal sealed record ObsConnectionRecord(string Host, int Port, string? ProtectedPassword);

internal sealed record RegisteredAppRecord(Guid Id, string DisplayName, string ExecutablePath);
