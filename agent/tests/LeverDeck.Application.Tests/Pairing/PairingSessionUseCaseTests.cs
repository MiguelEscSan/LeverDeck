using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Application.Tests.Fakes;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Tests.Pairing;

public sealed class PairingSessionUseCaseTests
{
    private readonly Harness _harness = new();

    private Task<Result> StartPairing() => _harness.StartPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);

    // StartPairing

    [Fact]
    public async Task StartPairing_NoSession_StoresANewSessionAndPublishesPairingSessionChanged()
    {
        _harness.Clock.Advance(TimeSpan.FromMinutes(3));

        var result = await _harness.StartPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<PairingSession>(_harness.Pairing.Current);
        Assert.Equal(new PairingCode("100001"), session.Code);
        Assert.Equal(_harness.Clock.UtcNow, session.StartedAt);
        Assert.Equal([new PairingSessionChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task StartPairing_SessionAlreadyActive_ReplacesItWithANewCode()
    {
        await StartPairing();
        var first = _harness.Pairing.Current;

        await StartPairing();

        Assert.NotSame(first, _harness.Pairing.Current);
        Assert.Equal(new PairingCode("100002"), _harness.Pairing.Current!.Code);
    }

    // CancelPairing

    [Fact]
    public async Task CancelPairing_ActiveSession_ClearsItAndPublishesPairingSessionChanged()
    {
        await StartPairing();

        var result = await _harness.CancelPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_harness.Pairing.Current);
        Assert.Equal([new PairingSessionChanged(), new PairingSessionChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task CancelPairing_NoSession_SucceedsAsANoOp()
    {
        var result = await _harness.CancelPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_harness.Pairing.Current);
    }

    // GetPairingSession

    [Fact]
    public async Task GetPairingSession_NoSession_ReturnsNull()
    {
        var result = await _harness.GetPairingSession().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPairingSession_ActiveSession_ReturnsItWithoutSideEffects()
    {
        await StartPairing();
        var eventsBefore = _harness.Events.Published.Count;

        var result = await _harness.GetPairingSession().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Same(_harness.Pairing.Current, result.Value);
        Assert.Equal(eventsBefore, _harness.Events.Published.Count);
    }

    [Fact]
    public async Task GetPairingSession_ExpiredSession_ReturnsNullAndKeepsTheStore()
    {
        await StartPairing();
        _harness.Clock.Advance(PairingSession.Lifetime);

        var result = await _harness.GetPairingSession().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Null(result.Value);
        Assert.NotNull(_harness.Pairing.Current);
    }

    [Fact]
    public async Task GetPairingSession_ExhaustedSession_ReturnsNull()
    {
        await StartPairing();
        for (var attempt = 0; attempt < PairingSession.MaxFailedAttempts; attempt++)
        {
            _harness.Pairing.Current!.Redeem(new PairingCode("999999"), FakeClock.Start);
        }

        var result = await _harness.GetPairingSession().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Null(result.Value);
    }
}
