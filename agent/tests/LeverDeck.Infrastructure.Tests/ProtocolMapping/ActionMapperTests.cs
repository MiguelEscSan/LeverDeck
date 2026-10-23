using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class ActionMapperTests
{
    private static readonly Guid _id = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly DomainAction[] _actions =
    [
        new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, VirtualKey.F5),
        new Hotkey(KeyModifiers.None, VirtualKey.PrintScreen),
        new MediaKey(MediaKeyKind.VolumeDown),
        new LaunchApp(new AppId(_id)),
        new OpenUrl(new Uri("https://example.com/path?q=1")),
        new ObsScene("Be right back"),
        new PlaySound(new LeverDeck.Domain.Sounds.SoundId("sound-0123456789abcdef0123456789abcdef")),
        new LaunchGame(GameStore.Steam, "570"),
        new LaunchGame(GameStore.Epic, "Fortnite"),
        new ChangePage(PageTarget.Next),
        new ChangePage(PageTarget.Previous),
        new ChangePage(PageTarget.At(3)),
        new SwitchProfile(new ProfileId(_id)),
    ];

    public static TheoryData<int> ActionIndexes() => [.. Enumerable.Range(0, _actions.Length)];

    [Fact]
    public void Samples_CoverEveryWhitelistedAction()
    {
        Assert.All(ActionCatalog.All, type => Assert.Contains(_actions, action => action.GetType() == type));
    }

    [Theory]
    [MemberData(nameof(ActionIndexes))]
    public void Action_SurvivesDomainToDtoToDomain(int index)
    {
        var action = _actions[index];

        var back = MappingAssert.Succeeds(ActionMapper.ToDomain(ActionMapper.ToDto(action)));

        Assert.Equal(action, back);
    }

    [Fact]
    public void Hotkey_ListsModifiersInCanonicalOrder()
    {
        var dto = Assert.IsType<HotkeyActionDto>(
            ActionMapper.ToDto(new Hotkey(KeyModifiers.Win | KeyModifiers.Shift | KeyModifiers.Ctrl, VirtualKey.D1)));

        Assert.Equal(new[] { KeyModifierDto.Ctrl, KeyModifierDto.Shift, KeyModifierDto.Win }, dto.Modifiers);
        Assert.Equal(VirtualKeyDto.D1, dto.Key);
    }

    [Fact]
    public void ChangePage_CarriesAnIndexOnlyForASpecificPage()
    {
        Assert.Equal(new ChangePageActionDto(PageTargetDto.Next, null), ActionMapper.ToDto(new ChangePage(PageTarget.Next)));
        Assert.Equal(new ChangePageActionDto(PageTargetDto.Specific, 2), ActionMapper.ToDto(new ChangePage(PageTarget.At(2))));
    }

    [Fact]
    public void EveryWireEnum_MatchesItsDomainEnum()
    {
        Assert.Equal(Enum.GetNames<VirtualKey>(), Enum.GetNames<VirtualKeyDto>());
        Assert.Equal(Enum.GetNames<MediaKeyKind>(), Enum.GetNames<MediaKeyDto>());
        Assert.Equal(Enum.GetNames<GameStore>(), Enum.GetNames<GameStoreDto>());
        Assert.Equal(Enum.GetNames<PageTargetKind>(), Enum.GetNames<PageTargetDto>());
        Assert.Equal(Enum.GetNames<KeyModifiers>().Where(name => name != nameof(KeyModifiers.None)), Enum.GetNames<KeyModifierDto>());
    }

    public static TheoryData<ActionDto?> InvalidActions() =>
    [
        null,
        new HotkeyActionDto(null!, VirtualKeyDto.A),
        new HotkeyActionDto([(KeyModifierDto)42], VirtualKeyDto.A),
        new HotkeyActionDto([], (VirtualKeyDto)999),
        new MediaKeyActionDto((MediaKeyDto)99),
        new LaunchAppActionDto(Guid.Empty),
        new OpenUrlActionDto("ftp://example.com/file"),
        new OpenUrlActionDto("not a url"),
        new OpenUrlActionDto("/relative/path"),
        new OpenUrlActionDto(null!),
        new OpenUrlActionDto($"https://example.com/{new string('a', 2100)}"),
        new ObsSceneActionDto("   "),
        new PlaySoundActionDto("sound-../../settings"),
        new PlaySoundActionDto("sound-0123456789ABCDEF0123456789abcdef"),
        new PlaySoundActionDto(null!),
        new ObsSceneActionDto(null!),
        new LaunchGameActionDto(GameStoreDto.Steam, "abc"),
        new LaunchGameActionDto(GameStoreDto.Epic, "has space"),
        new LaunchGameActionDto((GameStoreDto)7, "1"),
        new LaunchGameActionDto(GameStoreDto.Steam, null!),
        new ChangePageActionDto(PageTargetDto.Specific, null),
        new ChangePageActionDto(PageTargetDto.Specific, -1),
        new ChangePageActionDto(PageTargetDto.Next, 1),
        new ChangePageActionDto((PageTargetDto)9, null),
        new SwitchProfileActionDto(Guid.Empty),
    ];

    [Theory]
    [MemberData(nameof(InvalidActions))]
    public void InvalidAction_BecomesValidationFailure(ActionDto? dto)
    {
        MappingAssert.IsValidationFailure(ActionMapper.ToDomain(dto!));
    }
}
