using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;
using LeverDeck.Protocol.Dtos;
using static LeverDeck.Infrastructure.ProtocolMapping.EnumMapping;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Maps every whitelisted action to its <see cref="ActionDto"/> and back.</summary>
public static class ActionMapper
{
    // Canonical order of the modifiers on the wire.
    private static readonly (KeyModifiers Flag, KeyModifierDto Dto)[] _modifiers =
    [
        (KeyModifiers.Ctrl, KeyModifierDto.Ctrl),
        (KeyModifiers.Shift, KeyModifierDto.Shift),
        (KeyModifiers.Alt, KeyModifierDto.Alt),
        (KeyModifiers.Win, KeyModifierDto.Win),
    ];

    public static ActionDto ToDto(DomainAction action) => action switch
    {
        Hotkey hotkey => new HotkeyActionDto(ToDtos(hotkey.Modifiers), Convert<VirtualKey, VirtualKeyDto>(hotkey.Key)),
        MediaKey mediaKey => new MediaKeyActionDto(Convert<MediaKeyKind, MediaKeyDto>(mediaKey.Key)),
        LaunchApp launchApp => new LaunchAppActionDto(launchApp.App.Value),
        OpenUrl openUrl => new OpenUrlActionDto(openUrl.Url.AbsoluteUri),
        ObsScene obsScene => new ObsSceneActionDto(obsScene.SceneName),
        LaunchGame launchGame => new LaunchGameActionDto(Convert<GameStore, GameStoreDto>(launchGame.Store), launchGame.GameId),
        ChangePage changePage => new ChangePageActionDto(
            Convert<PageTargetKind, PageTargetDto>(changePage.Destination.Kind),
            changePage.Destination.Index),
        SwitchProfile switchProfile => new SwitchProfileActionDto(switchProfile.Profile.Value),
        null => throw new ArgumentNullException(nameof(action)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Unknown action."),
    };

    /// <summary>Builds the domain action; an invalid DTO becomes a <c>validationFailed</c> result.</summary>
    public static Result<DomainAction> ToDomain(ActionDto dto) => MappingBoundary.Run(() => Create(dto));

    /// <summary>Throwing variant for mappers that already run inside a <see cref="MappingBoundary"/>.</summary>
    internal static DomainAction Create(ActionDto dto) => MappingBoundary.Required(dto, "action") switch
    {
        HotkeyActionDto hotkey => new Hotkey(ToFlags(hotkey.Modifiers), Convert<VirtualKeyDto, VirtualKey>(hotkey.Key)),
        MediaKeyActionDto mediaKey => new MediaKey(Convert<MediaKeyDto, MediaKeyKind>(mediaKey.Key)),
        LaunchAppActionDto launchApp => new LaunchApp(new AppId(launchApp.AppId)),
        OpenUrlActionDto openUrl => new OpenUrl(ToUri(openUrl.Url)),
        ObsSceneActionDto obsScene => new ObsScene(obsScene.SceneName),
        LaunchGameActionDto launchGame => new LaunchGame(Convert<GameStoreDto, GameStore>(launchGame.Store), launchGame.GameId),
        ChangePageActionDto changePage => new ChangePage(ToTarget(changePage)),
        SwitchProfileActionDto switchProfile => new SwitchProfile(new ProfileId(switchProfile.ProfileId)),
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.GetType().Name, "Unknown action type."),
    };

    private static List<KeyModifierDto> ToDtos(KeyModifiers modifiers) =>
        [.. _modifiers.Where(modifier => modifiers.HasFlag(modifier.Flag)).Select(modifier => modifier.Dto)];

    private static KeyModifiers ToFlags(IReadOnlyList<KeyModifierDto> modifiers)
    {
        var flags = KeyModifiers.None;
        foreach (var modifier in MappingBoundary.Required(modifiers, "modifiers"))
        {
            var match = Array.FindIndex(_modifiers, candidate => candidate.Dto == modifier);
            if (match < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(modifiers), modifier, $"Unknown key modifier '{modifier}'.");
            }

            flags |= _modifiers[match].Flag;
        }

        return flags;
    }

    private static Uri ToUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri
            : throw new ArgumentException("URL must be absolute and use http or https.", nameof(url));

    private static PageTarget ToTarget(ChangePageActionDto dto) => (dto.Target, dto.PageIndex) switch
    {
        (PageTargetDto.Next, null) => PageTarget.Next,
        (PageTargetDto.Previous, null) => PageTarget.Previous,
        (PageTargetDto.Specific, { } index) => PageTarget.At(index),
        (PageTargetDto.Specific, null) => throw new ArgumentException("A specific page needs a pageIndex.", nameof(dto)),
        (PageTargetDto.Next or PageTargetDto.Previous, _) =>
            throw new ArgumentException("pageIndex is allowed only for a specific page.", nameof(dto)),
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Target, $"Unknown page target '{dto.Target}'."),
    };
}
