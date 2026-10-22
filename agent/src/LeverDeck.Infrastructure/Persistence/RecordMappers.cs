using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.Persistence.Records;
using LeverDeck.Infrastructure.Security;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>
/// Domain ⇄ file records. Reading goes through the domain constructors, so a stored value that breaks an invariant
/// throws, and <see cref="JsonDocumentFile{TDocument}"/> reports the file as invalid.
/// </summary>
internal static class ProfileRecordMapper
{
    public static ProfileRecord ToRecord(Profile profile) => new(
        profile.Id.Value,
        profile.Name.Value,
        profile.Color,
        new GridSizeRecord(profile.GridSize.Columns, profile.GridSize.Rows),
        [.. profile.Pages.Select(page => new PageRecord([.. page.Buttons.Select(ToRecord)]))]);

    public static Profile ToDomain(ProfileRecord record) => Profile.Restore(
        new ProfileId(record.Id),
        new ProfileName(record.Name),
        record.Color,
        GridSize.From(record.GridSize.Columns, record.GridSize.Rows),
        record.Pages.Select((page, index) => new Page(index, page.Buttons.Select(ToDomain))));

    private static ButtonRecord ToRecord(Button button) => new(
        button.Id.Value,
        button.Position.Row,
        button.Position.Column,
        button.Appearance.Label,
        button.Appearance.Color,
        button.Appearance.Icon.Value,
        ActionRecordMapper.ToRecord(button.Action));

    private static Button ToDomain(ButtonRecord record) => new(
        new ButtonId(record.Id),
        new GridPosition(record.Row, record.Column),
        new ButtonAppearance(record.Label, record.Color, new IconId(record.Icon)),
        ActionRecordMapper.ToDomain(record.Action));
}

internal static class ActionRecordMapper
{
    public static ActionRecord ToRecord(DomainAction action) => action switch
    {
        Hotkey hotkey => new HotkeyRecord(hotkey.Modifiers, hotkey.Key),
        MediaKey mediaKey => new MediaKeyRecord(mediaKey.Key),
        LaunchApp launchApp => new LaunchAppRecord(launchApp.App.Value),
        OpenUrl openUrl => new OpenUrlRecord(openUrl.Url.AbsoluteUri),
        ObsScene obsScene => new ObsSceneRecord(obsScene.SceneName),
        LaunchGame launchGame => new LaunchGameRecord(launchGame.Store, launchGame.GameId),
        ChangePage changePage => new ChangePageRecord(changePage.Destination.Kind, changePage.Destination.Index),
        SwitchProfile switchProfile => new SwitchProfileRecord(switchProfile.Profile.Value),
        null => throw new ArgumentNullException(nameof(action)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Unknown action."),
    };

    public static DomainAction ToDomain(ActionRecord record) => record switch
    {
        HotkeyRecord hotkey => new Hotkey(hotkey.Modifiers, hotkey.Key),
        MediaKeyRecord mediaKey => new MediaKey(mediaKey.Key),
        LaunchAppRecord launchApp => new LaunchApp(new AppId(launchApp.AppId)),
        OpenUrlRecord openUrl => new OpenUrl(new Uri(openUrl.Url, UriKind.Absolute)),
        ObsSceneRecord obsScene => new ObsScene(obsScene.SceneName),
        LaunchGameRecord launchGame => new LaunchGame(launchGame.Store, launchGame.GameId),
        ChangePageRecord changePage => new ChangePage(ToTarget(changePage)),
        SwitchProfileRecord switchProfile => new SwitchProfile(new ProfileId(switchProfile.ProfileId)),
        null => throw new ArgumentNullException(nameof(record)),
        _ => throw new ArgumentOutOfRangeException(nameof(record), record.GetType().Name, "Unknown action record."),
    };

    private static PageTarget ToTarget(ChangePageRecord record) => (record.Target, record.PageIndex) switch
    {
        (PageTargetKind.Next, null) => PageTarget.Next,
        (PageTargetKind.Previous, null) => PageTarget.Previous,
        (PageTargetKind.Specific, { } index) => PageTarget.At(index),
        _ => throw new ArgumentException($"Page target {record.Target} with index {record.PageIndex} is invalid.", nameof(record)),
    };
}

internal static class DeviceRecordMapper
{
    public static DeviceRecord ToRecord(Device device) => new(
        device.Id.Value,
        device.Name.Value,
        device.TokenHash.Value,
        device.PairedAt,
        device.LastSeenAt,
        device.RevokedAt);

    public static Device ToDomain(DeviceRecord record) => Device.Restore(
        new DeviceId(record.Id),
        new DeviceName(record.Name),
        new TokenHash(record.TokenHash),
        record.PairedAt,
        record.LastSeenAt,
        record.RevokedAt);
}

internal static class SettingsRecordMapper
{
    public static SettingsDocument ToDocument(AgentSettings settings, ISecretProtector protector) => new(
        SettingsDocument.CurrentVersion,
        settings.StartWithWindows,
        new GridSizeRecord(settings.DefaultGridSize.Columns, settings.DefaultGridSize.Rows),
        settings.ObsConnection is { } obs
            ? new ObsConnectionRecord(obs.Host, obs.Port, obs.Password is { } password ? protector.Protect(password) : null)
            : null,
        [.. settings.RegisteredApps.Select(app => new RegisteredAppRecord(app.Id.Value, app.DisplayName, app.ExecutablePath))]);

    public static AgentSettings ToDomain(SettingsDocument document, ISecretProtector protector) => AgentSettings.Restore(
        document.StartWithWindows,
        GridSize.From(document.DefaultGridSize.Columns, document.DefaultGridSize.Rows),
        document.Obs is { } obs
            ? new ObsConnection(obs.Host, obs.Port, obs.ProtectedPassword is { } password ? protector.Unprotect(password) : null)
            : null,
        document.RegisteredApps.Select(app => new RegisteredApp(new AppId(app.Id), app.DisplayName, app.ExecutablePath)));
}
