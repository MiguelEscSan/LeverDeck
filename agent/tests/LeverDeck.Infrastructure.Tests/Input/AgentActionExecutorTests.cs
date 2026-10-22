using LeverDeck.Application.Actions.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.Input;
using LeverDeck.Infrastructure.Tests.Persistence;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Infrastructure.Tests.Input;

/// <summary>
/// Routing of the agent's executor with a recording keyboard executor and shell: no test sends input or starts
/// anything on the PC.
/// </summary>
public sealed class AgentActionExecutorTests
{
    private static readonly string _appPath = Path.Combine(Path.GetTempPath(), "leverdeck-tests", "Game Tool.exe");

    private sealed class RecordingKeys : IActionExecutor
    {
        public List<DomainAction> Executed { get; } = [];

        public Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken)
        {
            Executed.Add(action);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RecordingShell(bool accept = true) : IShellLauncher
    {
        public List<string> Calls { get; } = [];

        public bool Open(Uri uri)
        {
            Calls.Add($"open {uri.AbsoluteUri}");
            return accept;
        }

        public bool Start(string executablePath)
        {
            Calls.Add($"start {executablePath}");
            return accept;
        }
    }

    private sealed class FixedSettings(AgentSettings settings) : ISettingsRepository
    {
        public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(settings);

        public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The executor never writes settings.");
    }

    private static AgentSettings SettingsWith(params AppId[] apps) => AgentSettings.Restore(
        false,
        GridSize.FiveByThree,
        null,
        apps.Select((id, index) => new RegisteredApp(id, $"Tool {index}", index == 0 ? _appPath : _appPath + index)));

    private static (AgentActionExecutor Executor, RecordingKeys Keys, RecordingShell Shell) Create(
        AgentSettings? settings = null,
        bool shellAccepts = true,
        bool fileExists = true)
    {
        var keys = new RecordingKeys();
        var shell = new RecordingShell(shellAccepts);
        var executor = new AgentActionExecutor(
            keys,
            shell,
            new FixedSettings(settings ?? AgentSettings.CreateDefault()),
            _ => fileExists);
        return (executor, keys, shell);
    }

    public static TheoryData<DomainAction> Samples() => [.. JsonProfileRepositoryTests.DomainActionSamples()];

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task EverySample_IsRoutedAsItsCoverageSays(DomainAction action)
    {
        var settings = action is LaunchApp launch ? SettingsWith(launch.App) : AgentSettings.CreateDefault();
        var (executor, keys, shell) = Create(settings);

        var result = await executor.ExecuteAsync(action, CancellationToken.None);

        switch (ActionExecutorCoverage.ByActionType[action.GetType()])
        {
            case ActionSupport.Executable:
                Assert.True(result.IsSuccess, result.Error?.ToString());
                Assert.Equal(1, keys.Executed.Count + shell.Calls.Count);
                break;
            case ActionSupport.NotExecutableYet:
            case ActionSupport.ClientOnly:
                Assert.Equal(DomainErrorCode.ActionNotExecutable, result.Error?.Code);
                Assert.Empty(keys.Executed);
                Assert.Empty(shell.Calls);
                break;
            default:
                Assert.Fail($"Unknown support for {action.GetType().Name}.");
                break;
        }
    }

    [Fact]
    public void Samples_CoverEveryActionType()
    {
        Assert.Equal(
            ActionCatalog.All.OrderBy(type => type.Name),
            JsonProfileRepositoryTests.DomainActionSamples().Select(action => action.GetType()).Distinct().OrderBy(type => type.Name));
    }

    [Fact]
    public async Task Keys_GoToTheKeyboardExecutor()
    {
        var (executor, keys, shell) = Create();
        var hotkey = new Hotkey(KeyModifiers.Ctrl, VirtualKey.C);

        await executor.ExecuteAsync(hotkey, CancellationToken.None);

        Assert.Equal([hotkey], keys.Executed);
        Assert.Empty(shell.Calls);
    }

    [Fact]
    public async Task OpenUrl_OpensTheLinkWithTheShell()
    {
        var (executor, _, shell) = Create();

        var result = await executor.ExecuteAsync(new OpenUrl(new Uri("https://example.com/a b?q=1")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["open https://example.com/a%20b?q=1"], shell.Calls);
    }

    [Theory]
    [InlineData(GameStore.Steam, "570", "open steam://rungameid/570")]
    [InlineData(GameStore.Epic, "Fortnite", "open com.epicgames.launcher://apps/Fortnite?action=launch&silent=true")]
    public async Task LaunchGame_OpensTheStoreUri(GameStore store, string gameId, string expected)
    {
        var (executor, _, shell) = Create();

        var result = await executor.ExecuteAsync(new LaunchGame(store, gameId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([expected], shell.Calls);
    }

    [Fact]
    public async Task LaunchApp_StartsTheRegisteredPath()
    {
        var app = AppId.New();
        var (executor, _, shell) = Create(SettingsWith(app));

        var result = await executor.ExecuteAsync(new LaunchApp(app), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([$"start {_appPath}"], shell.Calls);
    }

    [Fact]
    public async Task LaunchApp_UnregisteredApp_IsNotExecutable()
    {
        var (executor, _, shell) = Create(SettingsWith(AppId.New()));

        var result = await executor.ExecuteAsync(new LaunchApp(AppId.New()), CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionNotExecutable, result.Error?.Code);
        Assert.Empty(shell.Calls);
    }

    [Fact]
    public async Task LaunchApp_MissingExecutable_FailsWithoutStartingAnything()
    {
        var app = AppId.New();
        var (executor, _, shell) = Create(SettingsWith(app), fileExists: false);

        var result = await executor.ExecuteAsync(new LaunchApp(app), CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
        Assert.Contains("is no longer at", result.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(shell.Calls);
    }

    [Fact]
    public async Task ShellRefusal_FailsWithActionFailed()
    {
        var (executor, _, _) = Create(shellAccepts: false);

        var result = await executor.ExecuteAsync(new LaunchGame(GameStore.Steam, "570"), CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
        Assert.Contains("Steam", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledToken_RunsNothing()
    {
        var (executor, keys, shell) = Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(new OpenUrl(new Uri("https://example.com")), cancellation.Token));
        Assert.Empty(keys.Executed);
        Assert.Empty(shell.Calls);
    }
}
