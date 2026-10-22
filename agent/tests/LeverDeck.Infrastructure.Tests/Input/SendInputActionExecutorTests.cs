using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.Input;
using LeverDeck.Infrastructure.Tests.Persistence;

namespace LeverDeck.Infrastructure.Tests.Input;

/// <summary>
/// Routing of the executor, with a recording keyboard: no test ever reaches <c>SendInput</c>.
/// </summary>
public sealed class SendInputActionExecutorTests
{
    private sealed class RecordingKeyboard(bool accept = true) : IKeyboardInput
    {
        public List<IReadOnlyList<KeyStroke>> Sent { get; } = [];

        public bool Send(IReadOnlyList<KeyStroke> strokes)
        {
            Sent.Add(strokes);
            return accept;
        }
    }

    public static TheoryData<LeverDeck.Domain.Actions.Action> Samples() => [.. JsonProfileRepositoryTests.DomainActionSamples()];

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task EverySample_IsRoutedAsItsCoverageSays(LeverDeck.Domain.Actions.Action action)
    {
        var keyboard = new RecordingKeyboard();
        var executor = new SendInputActionExecutor(keyboard);

        var result = await executor.ExecuteAsync(action, CancellationToken.None);

        switch (ActionExecutorCoverage.ByActionType[action.GetType()])
        {
            case ActionSupport.Executable:
                Assert.True(result.IsSuccess);
                Assert.Single(keyboard.Sent);
                break;
            case ActionSupport.NotExecutableYet:
                Assert.Equal(DomainErrorCode.ActionNotExecutable, result.Error?.Code);
                Assert.Contains("Phase 7", result.Error?.Message, StringComparison.Ordinal);
                Assert.Empty(keyboard.Sent);
                break;
            case ActionSupport.ClientOnly:
                Assert.Equal(DomainErrorCode.ActionNotExecutable, result.Error?.Code);
                Assert.Empty(keyboard.Sent);
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
    public async Task Hotkey_SendsItsKeySequence()
    {
        var keyboard = new RecordingKeyboard();
        var hotkey = new Hotkey(KeyModifiers.Alt, VirtualKey.Tab);

        await new SendInputActionExecutor(keyboard).ExecuteAsync(hotkey, CancellationToken.None);

        Assert.Equal(KeySequence.For(hotkey), Assert.Single(keyboard.Sent));
    }

    [Fact]
    public async Task RejectedInput_FailsWithActionFailed()
    {
        var executor = new SendInputActionExecutor(new RecordingKeyboard(accept: false));

        var result = await executor.ExecuteAsync(new MediaKey(MediaKeyKind.Mute), CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
    }

    [Fact]
    public async Task CancelledToken_SendsNothing()
    {
        var keyboard = new RecordingKeyboard();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SendInputActionExecutor(keyboard).ExecuteAsync(new MediaKey(MediaKeyKind.Mute), cancellation.Token));
        Assert.Empty(keyboard.Sent);
    }
}
