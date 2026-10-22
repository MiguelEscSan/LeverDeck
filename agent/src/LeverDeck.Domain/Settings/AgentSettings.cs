using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Settings;

/// <summary>
/// Aggregate root for the agent's own configuration. Registered apps have unique ids and unique executable paths
/// (compared case-insensitively, as on Windows). Whether an app is still used by a profile is a use-case concern.
/// </summary>
public sealed class AgentSettings
{
    private static readonly StringComparer _pathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly List<RegisteredApp> _apps;

    private AgentSettings(bool startWithWindows, GridSize defaultGridSize, ObsConnection? obsConnection, List<RegisteredApp> apps)
    {
        StartWithWindows = startWithWindows;
        DefaultGridSize = defaultGridSize;
        ObsConnection = obsConnection;
        _apps = apps;
    }

    public bool StartWithWindows { get; private set; }

    public GridSize DefaultGridSize { get; private set; }

    public ObsConnection? ObsConnection { get; private set; }

    public IReadOnlyList<RegisteredApp> RegisteredApps => _apps;

    /// <summary>A new instance with the defaults: no autostart, 5×3 grid, no OBS and no registered apps.</summary>
    public static AgentSettings CreateDefault() => new(false, GridSize.FiveByThree, null, []);

    /// <summary>Rebuilds the settings from stored state, validating every invariant.</summary>
    public static AgentSettings Restore(
        bool startWithWindows,
        GridSize defaultGridSize,
        ObsConnection? obsConnection,
        IEnumerable<RegisteredApp> registeredApps)
    {
        ArgumentNullException.ThrowIfNull(defaultGridSize);
        ArgumentNullException.ThrowIfNull(registeredApps);

        List<RegisteredApp> apps = [.. registeredApps];
        foreach (var app in apps)
        {
            ArgumentNullException.ThrowIfNull(app, nameof(registeredApps));
        }

        Invariant.Require(
            apps.Select(app => app.Id).Distinct().Count() == apps.Count,
            DomainErrorCode.ValidationFailed,
            "Registered app ids must be unique.");
        Invariant.Require(
            apps.Select(app => app.ExecutablePath).Distinct(_pathComparer).Count() == apps.Count,
            DomainErrorCode.ValidationFailed,
            "Registered app paths must be unique.");

        return new AgentSettings(startWithWindows, defaultGridSize, obsConnection, apps);
    }

    public void SetStartWithWindows(bool startWithWindows) => StartWithWindows = startWithWindows;

    public void ChangeDefaultGridSize(GridSize gridSize)
    {
        ArgumentNullException.ThrowIfNull(gridSize);
        DefaultGridSize = gridSize;
    }

    /// <summary>Sets the OBS connection; <see langword="null"/> clears it.</summary>
    public void ConfigureObs(ObsConnection? obsConnection) => ObsConnection = obsConnection;

    /// <summary>Registers an executable under a new id. A path already registered (ignoring case) is rejected.</summary>
    public AppId RegisterApp(string displayName, string executablePath)
    {
        var app = new RegisteredApp(AppId.New(), displayName, executablePath);
        Invariant.Require(
            !_apps.Any(existing => _pathComparer.Equals(existing.ExecutablePath, app.ExecutablePath)),
            DomainErrorCode.ValidationFailed,
            "This executable is already registered.");

        _apps.Add(app);
        return app.Id;
    }

    public void UnregisterApp(AppId appId)
    {
        var app = FindApp(appId)
            ?? throw new InvariantViolationException(DomainErrorCode.ValidationFailed, $"App {appId} is not registered.");
        _apps.Remove(app);
    }

    public RegisteredApp? FindApp(AppId appId) => _apps.FirstOrDefault(app => app.Id == appId);
}
