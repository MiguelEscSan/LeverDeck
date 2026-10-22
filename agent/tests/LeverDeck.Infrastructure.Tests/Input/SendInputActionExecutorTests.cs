using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.Input;

namespace LeverDeck.Infrastructure.Tests.Input;

/// <summary>
/// The keyboard half of the executor, with a recording keyboard: no test ever reaches <c>SendInput</c>.
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

    [Theory]
    [MemberData(nameof(AgentActionExecutorTests.Samples), MemberType = typeof(AgentActionExecutorTests))]
    public async Task AnyActionButAKey_FailsWithoutSendingInput(LeverDeck.Domain.Actions.Action action)
    {
        var keyboard = new RecordingKeyboard();

        var result = await new SendInputActionExecutor(keyboard).ExecuteAsync(action, CancellationToken.None);

        if (action is Hotkey or MediaKey)
        {
            Assert.True(result.IsSuccess);
            Assert.Single(keyboard.Sent);
        }
        else
        {
            Assert.Equal(DomainErrorCode.ActionNotExecutable, result.Error?.Code);
            Assert.Empty(keyboard.Sent);
        }
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
