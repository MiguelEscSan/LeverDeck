using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Protocol.Tests;

/// <summary>One instance of every concrete protocol type. <see cref="CoverageTests"/> keeps the list complete.</summary>
internal static class Samples
{
    public static readonly Guid ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ButtonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DeviceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid AppId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly DateTimeOffset PairedAt = new(2026, 10, 22, 8, 30, 0, 125, TimeSpan.Zero);

    public static HotkeyActionDto Hotkey { get; } = new([KeyModifierDto.Ctrl, KeyModifierDto.Shift], VirtualKeyDto.PageUp);

    public static ButtonDto Button { get; } = new(ButtonId, 0, 1, "Copy", KeyColorDto.Teal, "content-copy", Hotkey);

    public static ProfileDto Profile { get; } =
        new(ProfileId, "Streaming", KeyColorDto.Purple, new GridDto(5, 3), [new PageDto(0, [Button])]);

    public static ProfileSummaryDto Summary { get; } = new(ProfileId, "Streaming", KeyColorDto.Purple);

    public static DeviceDto Device { get; } = new(DeviceId, "Pixel", PairedAt, PairedAt.AddHours(2), true, false);

    public static PairingSessionDto Session { get; } = new(
        "123456",
        "http://leverdeck.local:8765/pair?code=123456",
        "http://192.168.1.20:8765/pair?code=123456",
        PairedAt.AddMinutes(5));

    public static RegisteredAppDto App { get; } = new(AppId, "Notepad", @"C:\Windows\notepad.exe");

    public static SettingsDto Settings { get; } =
        new(true, new GridDto(8, 4), new ObsSettingsDto("localhost", 4455, true), [App]);

    public static SettingsUpdateDto SettingsUpdate { get; } =
        new(false, new GridDto(3, 3), new ObsUpdateDto("localhost", 4455, "secret"));

    public static InstalledGameDto Game { get; } = new(GameStoreDto.Steam, "570", "Dota 2");

    public const string IconId = "custom-0123456789abcdef0123456789abcdef";

    public static IReadOnlyList<ActionDto> Actions { get; } =
    [
        Hotkey,
        new MediaKeyActionDto(MediaKeyDto.VolumeUp),
        new LaunchAppActionDto(AppId),
        new OpenUrlActionDto("https://example.com/"),
        new ObsSceneActionDto("Gameplay"),
        new LaunchGameActionDto(GameStoreDto.Steam, "570"),
        new ChangePageActionDto(PageTargetDto.Specific, 2),
        new SwitchProfileActionDto(ProfileId),
    ];

    public static IReadOnlyList<Message> Messages { get; } =
    [
        new ConnectDeviceCommand("r1", new ConnectDevicePayload("token", ProtocolInfo.ProtocolVersion)),
        new ExecuteActionCommand("r2", new ExecuteActionPayload(ProfileId, 0, ButtonId)),
        new SaveProfileCommand("r3", new ProfilePayload(Profile)),
        new DeleteProfileCommand("r4", new ProfileIdPayload(ProfileId)),
        new StartPairingCommand("r5"),
        new CancelPairingCommand("r6"),
        new RenameDeviceCommand("r7", new RenameDevicePayload(DeviceId, "Tablet")),
        new RevokeDeviceCommand("r8", new DeviceIdPayload(DeviceId)),
        new UpdateSettingsCommand("r9", new UpdateSettingsPayload(SettingsUpdate)),
        new RegisterAppCommand("r10", new RegisterAppPayload("Notepad", @"C:\Windows\notepad.exe")),
        new UnregisterAppCommand("r11", new AppIdPayload(AppId)),
        new SaveIconCommand("r12", new SaveIconPayload(IconId, "iVBORw0KGgo=")),
        new DeleteIconCommand("r13", new IconIdPayload(IconId)),
        new GetProfilesQuery("q1"),
        new GetProfileQuery("q2", new ProfileIdPayload(ProfileId)),
        new GetDevicesQuery("q3"),
        new GetPairingSessionQuery("q4"),
        new GetSettingsQuery("q5"),
        new GetAgentStatusQuery("q6"),
        new GetInstalledGamesQuery("q7"),
        new GetIconsQuery("q8"),
        new ProfilesResult("q1", new ProfilesPayload([Summary])),
        new ProfileResult("q2", new ProfilePayload(Profile)),
        new DevicesResult("q3", new DevicesPayload([Device])),
        new PairingSessionResult("q4", new PairingSessionPayload(Session)),
        new SettingsResult("q5", new SettingsPayload(Settings)),
        new AgentStatusResult("q6", new AgentStatusPayload("1.0.0", ProtocolInfo.ProtocolVersion, false)),
        new InstalledGamesResult("q7", new InstalledGamesPayload([Game])),
        new IconsResult("q8", new IconsPayload([IconId])),
        new AckMessage("r1"),
        new AckMessage("r10", new AppRegisteredPayload(AppId)),
        new ErrorMessage("r2", ErrorCodeDto.NotFound, "Profile does not exist."),
        new ProfileUpdatedEvent(new ProfilePayload(Profile)),
        new ProfileDeletedEvent(new ProfileIdPayload(ProfileId)),
        new DevicesChangedEvent(new DevicesPayload([Device])),
        new DeviceRevokedEvent(new DeviceIdPayload(DeviceId)),
        new PairingSessionChangedEvent(new PairingSessionPayload(null)),
        new SettingsUpdatedEvent(new SettingsPayload(Settings)),
        new ObsSceneChangedEvent(new ObsSceneChangedPayload("Gameplay")),
        new IconsChangedEvent(new IconsPayload([IconId])),
    ];

    /// <summary>Every DTO and payload on its own, plus the HTTP pairing bodies.</summary>
    public static IReadOnlyList<object> Dtos { get; } =
    [
        Profile,
        Summary,
        Button,
        new PageDto(1, []),
        new GridDto(3, 3),
        Device,
        Session,
        Settings,
        SettingsUpdate,
        App,
        new ObsSettingsDto("localhost", 4455, false),
        new ObsUpdateDto("localhost", 4455, null),
        new ErrorDto(ErrorCodeDto.ValidationFailed, "Name is required."),
        new PairDeviceRequest("123456", "Pixel"),
        new PairDeviceResponse(DeviceId, "device-token"),
        EmptyPayload.Instance,
        new ConnectDevicePayload("token", 1),
        new ExecuteActionPayload(ProfileId, 0, ButtonId),
        new ProfileIdPayload(ProfileId),
        new ProfilePayload(Profile),
        new RenameDevicePayload(DeviceId, "Tablet"),
        new DeviceIdPayload(DeviceId),
        new UpdateSettingsPayload(SettingsUpdate),
        new RegisterAppPayload("Notepad", @"C:\Windows\notepad.exe"),
        new AppIdPayload(AppId),
        new AppRegisteredPayload(AppId),
        new ProfilesPayload([Summary]),
        new DevicesPayload([Device]),
        new PairingSessionPayload(Session),
        new SettingsPayload(Settings),
        new AgentStatusPayload("1.0.0", 1, true),
        new ObsSceneChangedPayload("Gameplay"),
        Game,
        new SaveIconPayload(IconId, "iVBORw0KGgo="),
        new IconIdPayload(IconId),
        new IconsPayload([IconId]),
        new InstalledGamesPayload([Game, new InstalledGameDto(GameStoreDto.Epic, "Fortnite", "Fortnite")]),
    ];

    public static IEnumerable<object> All => [.. Messages, .. Actions, .. Dtos];
}
