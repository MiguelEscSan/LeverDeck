using LeverDeck.Application.Actions;
using LeverDeck.Application.Devices;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Profiles;
using LeverDeck.Application.Settings;
using LeverDeck.Application.Status;
using LeverDeck.Application.Tests.Fakes;

namespace LeverDeck.Application.Tests;

/// <summary>One set of fakes per test, plus a factory for every use case wired to them.</summary>
internal sealed class Harness
{
    public InMemoryProfileRepository Profiles { get; } = new();

    public InMemoryDeviceRepository Devices { get; } = new();

    public InMemorySettingsRepository Settings { get; } = new();

    public InMemoryPairingSessionStore Pairing { get; } = new();

    public FakeTokenGenerator Tokens { get; } = new();

    public FakeTokenHasher Hasher { get; } = new();

    public FakeClock Clock { get; } = new();

    public RecordingEventPublisher Events { get; } = new();

    public RecordingActionExecutor Executor { get; } = new();

    /// <summary>Total writes to every repository and the pairing store, to prove a call had no side effects.</summary>
    public int RepositoryWrites => Profiles.Writes + Devices.Writes + Settings.Writes;

    public ConnectDevice ConnectDevice() => new(Devices, Hasher, Clock, Events);

    public ExecuteAction ExecuteAction() => new(Profiles, Settings, Executor);

    public SaveProfile SaveProfile() => new(Profiles, Settings, Events);

    public DeleteProfile DeleteProfile() => new(Profiles, Events);

    public StartPairing StartPairing() => new(Pairing, Tokens, Clock, Events);

    public CancelPairing CancelPairing() => new(Pairing, Events);

    public PairDevice PairDevice() => new(Pairing, Devices, Tokens, Hasher, Clock, Events);

    public RenameDevice RenameDevice() => new(Devices, Events);

    public RevokeDevice RevokeDevice() => new(Devices, Clock, Events);

    public UpdateSettings UpdateSettings() => new(Settings, Events);

    public RegisterApp RegisterApp() => new(Settings, Events);

    public UnregisterApp UnregisterApp() => new(Settings, Profiles, Events);

    public GetProfiles GetProfiles() => new(Profiles);

    public GetProfile GetProfile() => new(Profiles);

    public GetDevices GetDevices() => new(Devices);

    public GetPairingSession GetPairingSession() => new(Pairing, Clock);

    public GetSettings GetSettings() => new(Settings);

    public static GetAgentStatus GetAgentStatus() => new();
}
