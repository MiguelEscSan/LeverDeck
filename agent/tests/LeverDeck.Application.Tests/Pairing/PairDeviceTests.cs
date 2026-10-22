using LeverDeck.Application.Events;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Tests.Pairing;

public sealed class PairDeviceTests
{
    private const string Code = "100001";

    private readonly Harness _harness = new();

    private async Task<PairingSession> StartPairing()
    {
        await _harness.StartPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);
        return _harness.Pairing.Current!;
    }

    private Task<Result<PairedDevice>> Pair(string code, string name = "Phone") =>
        _harness.PairDevice().ExecuteAsync(Caller.Public, code, name, CancellationToken.None);

    private void AssertNoDeviceAndNoNewEvents(int eventsBefore)
    {
        Assert.Empty(_harness.Devices.All);
        Assert.Equal(eventsBefore, _harness.Events.Published.Count);
    }

    [Fact]
    public async Task ExecuteAsync_RightCode_CreatesDeviceWithHashedTokenAndReturnsTheRawTokenOnce()
    {
        await StartPairing();
        _harness.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await Pair(Code, " Pixel ");

        var device = Assert.Single(_harness.Devices.All);
        Assert.Equal(new PairedDevice(device.Id, "token-1"), result.Value);
        Assert.Equal(new DeviceName("Pixel"), device.Name);
        Assert.True(device.Matches(new TokenHash("hash:token-1")));
        Assert.Equal(_harness.Clock.UtcNow, device.PairedAt);
    }

    [Fact]
    public async Task ExecuteAsync_RightCode_ClearsTheSessionAndPublishesDevicesChangedThenPairingSessionChanged()
    {
        await StartPairing();

        await Pair(Code);

        Assert.Null(_harness.Pairing.Current);
        Assert.Equal<ApplicationEvent>(
            [new PairingSessionChanged(), new DevicesChanged(), new PairingSessionChanged()],
            _harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_PairedToken_CanConnect()
    {
        await StartPairing();
        var paired = (await Pair(Code)).Value;

        var connected = await _harness.ConnectDevice()
            .ExecuteAsync(Caller.Public, paired.RawToken, ApplicationInfo.ProtocolVersion, CancellationToken.None);

        Assert.Equal(paired.DeviceId, connected.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 10001")]
    public async Task ExecuteAsync_MalformedCode_FailsWithPairingCodeInvalidWithoutCountingAnAttempt(string code)
    {
        var session = await StartPairing();

        Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair(code));
        Assert.Equal(0, session.FailedAttempts);
        AssertNoDeviceAndNoNewEvents(eventsBefore: 1);
    }

    [Fact]
    public async Task ExecuteAsync_NoSession_FailsWithPairingCodeInvalid()
    {
        Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair(Code));
        AssertNoDeviceAndNoNewEvents(eventsBefore: 0);
    }

    [Fact]
    public async Task ExecuteAsync_WrongCode_FailsWithPairingCodeInvalidAndKeepsTheCountedSession()
    {
        var session = await StartPairing();

        Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair("999999"));
        Assert.Same(session, _harness.Pairing.Current);
        Assert.Equal(1, session.FailedAttempts);
        AssertNoDeviceAndNoNewEvents(eventsBefore: 1);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredCode_FailsWithPairingCodeExpired()
    {
        await StartPairing();
        _harness.Clock.Advance(PairingSession.Lifetime);

        Build.Fails(DomainErrorCode.PairingCodeExpired, await Pair(Code));
        AssertNoDeviceAndNoNewEvents(eventsBefore: 1);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDeviceName_FailsWithValidationFailedWithoutBurningTheSession()
    {
        var session = await StartPairing();

        Build.Fails(DomainErrorCode.ValidationFailed, await Pair(Code, "   "));
        Assert.Same(session, _harness.Pairing.Current);
        Assert.True(session.IsActive(_harness.Clock.UtcNow));
        Assert.Equal(0, session.FailedAttempts);
        AssertNoDeviceAndNoNewEvents(eventsBefore: 1);
    }
}
