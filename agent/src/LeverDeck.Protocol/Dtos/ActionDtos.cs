using System.Text.Json.Serialization;

namespace LeverDeck.Protocol.Dtos;

/// <summary>What a button does, discriminated by <c>type</c>. Unknown types are rejected on read.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HotkeyActionDto), "hotkey")]
[JsonDerivedType(typeof(MediaKeyActionDto), "mediaKey")]
[JsonDerivedType(typeof(LaunchAppActionDto), "launchApp")]
[JsonDerivedType(typeof(OpenUrlActionDto), "openUrl")]
[JsonDerivedType(typeof(ObsSceneActionDto), "obsScene")]
[JsonDerivedType(typeof(ObsStreamingActionDto), "obsStreaming")]
[JsonDerivedType(typeof(ObsRecordActionDto), "obsRecord")]
[JsonDerivedType(typeof(ObsInputMuteActionDto), "obsInputMute")]
[JsonDerivedType(typeof(ObsSourceVisibilityActionDto), "obsSourceVisibility")]
[JsonDerivedType(typeof(LaunchGameActionDto), "launchGame")]
[JsonDerivedType(typeof(ChangePageActionDto), "changePage")]
[JsonDerivedType(typeof(SwitchProfileActionDto), "switchProfile")]
public abstract record ActionDto
{
    private protected ActionDto()
    {
    }
}

public sealed record HotkeyActionDto(IReadOnlyList<KeyModifierDto> Modifiers, VirtualKeyDto Key) : ActionDto;

public sealed record MediaKeyActionDto(MediaKeyDto Key) : ActionDto;

public sealed record LaunchAppActionDto(Guid AppId) : ActionDto;

public sealed record OpenUrlActionDto(string Url) : ActionDto;

public sealed record ObsSceneActionDto(string SceneName) : ActionDto;

public sealed record LaunchGameActionDto(GameStoreDto Store, string GameId) : ActionDto;

/// <summary><see cref="PageIndex"/> is set only when <see cref="Target"/> is <see cref="PageTargetDto.Specific"/>.</summary>
public sealed record ChangePageActionDto(PageTargetDto Target, int? PageIndex) : ActionDto;

public sealed record SwitchProfileActionDto(Guid ProfileId) : ActionDto;

/// <summary>Starts, stops or toggles the OBS stream.</summary>
public sealed record ObsStreamingActionDto(ObsOutputCommandDto Command) : ActionDto;

/// <summary>Starts, stops or toggles the OBS recording.</summary>
public sealed record ObsRecordActionDto(ObsOutputCommandDto Command) : ActionDto;

public sealed record ObsInputMuteActionDto(string InputName, ObsMuteCommandDto Command) : ActionDto;

public sealed record ObsSourceVisibilityActionDto(string SceneName, string SourceName, ObsVisibilityCommandDto Command) : ActionDto;
