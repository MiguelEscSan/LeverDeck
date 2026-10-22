namespace LeverDeck.Protocol.Dtos;

/// <summary>The agent settings as read by the editor. The OBS password is never sent, only whether one is set.</summary>
public sealed record SettingsDto(
    bool StartWithWindows,
    GridDto DefaultGrid,
    ObsSettingsDto? Obs,
    IReadOnlyList<RegisteredAppDto> Apps);

public sealed record ObsSettingsDto(string Host, int Port, bool HasPassword);

public sealed record RegisteredAppDto(Guid AppId, string DisplayName, string Path);

/// <summary>
/// What <c>updateSettings</c> replaces. Registered apps are managed by <c>registerApp</c>/<c>unregisterApp</c>.
/// A null <see cref="Obs"/> clears the OBS connection.
/// </summary>
public sealed record SettingsUpdateDto(bool StartWithWindows, GridDto DefaultGrid, ObsUpdateDto? Obs);

/// <summary>
/// The OBS connection to store. <see cref="Password"/> is write-only: null keeps the stored password, an empty
/// string clears it and any other value replaces it.
/// </summary>
public sealed record ObsUpdateDto(string Host, int Port, string? Password)
{
    /// <summary>Redacted so the password never ends up in logs.</summary>
    public override string ToString() =>
        $"ObsUpdateDto {{ Host = {Host}, Port = {Port}, Password = {(Password is null ? "unchanged" : "***")} }}";
}
