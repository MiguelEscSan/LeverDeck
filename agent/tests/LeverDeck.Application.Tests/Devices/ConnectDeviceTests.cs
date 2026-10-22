using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Application.Tests.Fakes;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Tests.Devices;

public sealed class ConnectDeviceTests
{
    private readonly Harness _harness = new();

    private Task<Result<DeviceId>> Connect(string rawToken, int protocolVersion = ApplicationInfo.ProtocolVersion) =>
        _harness.ConnectDevice().ExecuteAsync(Caller.Public, rawToken, protocolVersion, CancellationToken.None);

    private void AssertNothingHappened()
    {
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public void ProtocolVersion_IsOne()
    {
        Assert.Equal(1, ApplicationInfo.ProtocolVersion);
    }

    [Fact]
    public async Task ExecuteAsync_KnownToken_ReturnsDeviceIdUpdatesLastSeenAndPublishesDevicesChanged()
    {
        var device = _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));
        _harness.Clock.Advance(TimeSpan.FromHours(1));

        var result = await Connect("secret");

        Assert.Equal(device.Id, result.Value);
        Assert.Equal(_harness.Clock.UtcNow, device.LastSeenAt);
        Assert.Equal(1, _harness.Devices.Writes);
        Assert.Equal([new DevicesChanged()], _harness.Events.Published);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ExecuteAsync_OtherProtocolVersion_FailsWithProtocolMismatch(int protocolVersion)
    {
        _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));

        Build.Fails(DomainErrorCode.ProtocolMismatch, await Connect("secret", protocolVersion));
        AssertNothingHappened();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownToken_FailsWithUnauthorized()
    {
        _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));

        Build.Fails(DomainErrorCode.Unauthorized, await Connect("guess"));
        AssertNothingHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_BlankToken_FailsWithUnauthorized(string rawToken)
    {
        Build.Fails(DomainErrorCode.Unauthorized, await Connect(rawToken));
        AssertNothingHappened();
    }

    [Fact]
    public async Task ExecuteAsync_RevokedDevice_FailsWithDeviceRevoked()
    {
        var device = _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));
        device.Revoke(FakeClock.Start);

        Build.Fails(DomainErrorCode.DeviceRevoked, await Connect("secret"));
        AssertNothingHappened();
    }
}
