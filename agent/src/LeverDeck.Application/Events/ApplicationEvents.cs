using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Events;

/// <summary>
/// Something the connected clients must hear about. Events carry only identifiers; their wire payloads are built in
/// Phase 3. Use cases publish them only after a successful change, never on a failure.
/// </summary>
public abstract record ApplicationEvent;

public sealed record ProfileUpdated(ProfileId ProfileId) : ApplicationEvent;

public sealed record ProfileDeleted(ProfileId ProfileId) : ApplicationEvent;

public sealed record DevicesChanged : ApplicationEvent;

public sealed record DeviceRevoked(DeviceId DeviceId) : ApplicationEvent;

public sealed record PairingSessionChanged : ApplicationEvent;

public sealed record SettingsUpdated : ApplicationEvent;

public sealed record IconsChanged : ApplicationEvent;

/// <summary>The program scene OBS switched to.</summary>
public sealed record ObsSceneChanged(string SceneName) : ApplicationEvent;

/// <summary>Whether OBS is streaming and recording after a change.</summary>
public sealed record ObsStateChanged(bool Streaming, bool Recording) : ApplicationEvent;
