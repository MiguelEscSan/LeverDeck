using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>The arguments of the <c>UpdateSettings</c> use case, built from an <see cref="SettingsUpdateDto"/>.</summary>
public sealed record SettingsUpdate(bool StartWithWindows, GridSize DefaultGridSize, ObsConnection? ObsConnection);

/// <summary>Maps the agent settings. The OBS password only ever travels inbound.</summary>
public static class SettingsMapper
{
    public static SettingsDto ToDto(AgentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SettingsDto(
            settings.StartWithWindows,
            GridMapper.ToDto(settings.DefaultGridSize),
            settings.ObsConnection is { } obs ? new ObsSettingsDto(obs.Host, obs.Port, obs.HasPassword) : null,
            [.. settings.RegisteredApps.Select(app => new RegisteredAppDto(app.Id.Value, app.DisplayName, app.ExecutablePath))]);
    }

    /// <summary>
    /// Builds the update. A null password keeps <paramref name="current"/>'s, an empty one clears it; an invalid DTO
    /// becomes a <c>validationFailed</c> result.
    /// </summary>
    public static Result<SettingsUpdate> ToUpdate(SettingsUpdateDto dto, ObsConnection? current) => MappingBoundary.Run(() =>
    {
        MappingBoundary.Required(dto, "settings");
        var obs = dto.Obs is { } update ? new ObsConnection(update.Host, update.Port, update.Password ?? current?.Password) : null;
        return new SettingsUpdate(dto.StartWithWindows, GridMapper.Create(dto.DefaultGrid), obs);
    });
}
