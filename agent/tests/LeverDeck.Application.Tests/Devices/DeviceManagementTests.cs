using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Application.Tests.Fakes;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Tests.Devices;

public sealed class DeviceManagementTests
{
    private readonly Harness _harness = new();

    // RenameDevice

    [Fact]
    public async Task RenameDevice_KnownDevice_RenamesSavesAndPublishesDevicesChanged()
    {
        var device = _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));

        var result = await _harness.RenameDevice().ExecuteAsync(Caller.Admin, device.Id, "  Tablet ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DeviceName("Tablet"), device.Name);
        Assert.Equal(1, _harness.Devices.Writes);
        Assert.Equal([new DevicesChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task RenameDevice_UnknownDevice_FailsWithDeviceNotFound()
    {
        var result = await _harness.RenameDevice().ExecuteAsync(Caller.Admin, DeviceId.New(), "Tablet", CancellationToken.None);

        Build.Fails(DomainErrorCode.DeviceNotFound, result);
        Assert.Empty(_harness.Events.Published);
    }

    [Theory]
    [InlineData("")]
    [InlineData("This name is far too long to be a device name")]
    public async Task RenameDevice_InvalidName_FailsWithValidationFailedWithoutThrowing(string name)
    {
        var device = _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));

        var result = await _harness.RenameDevice().ExecuteAsync(Caller.Admin, device.Id, name, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Equal(new DeviceName("Phone"), device.Name);
        Assert.Equal(0, _harness.Devices.Writes);
        Assert.Empty(_harness.Events.Published);
    }

    // RevokeDevice

    [Fact]
    public async Task RevokeDevice_KnownDevice_RevokesSavesAndPublishesDeviceRevokedThenDevicesChanged()
    {
        var device = _harness.Devices.Add(Build.DeviceWithToken("secret", FakeClock.Start));
        _harness.Clock.Advance(TimeSpan.FromDays(1));

        var result = await _harness.RevokeDevice().ExecuteAsync(Caller.Admin, device.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_harness.Clock.UtcNow, device.RevokedAt);
        Assert.Equal(1, _harness.Devices.Writes);
        Assert.Equal<ApplicationEvent>([new DeviceRevoked(device.Id), new DevicesChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task RevokeDevice_UnknownDevice_FailsWithDeviceNotFound()
    {
        var result = await _harness.RevokeDevice().ExecuteAsync(Caller.Admin, DeviceId.New(), CancellationToken.None);

        Build.Fails(DomainErrorCode.DeviceNotFound, result);
        Assert.Empty(_harness.Events.Published);
    }

    // GetDevices

    [Fact]
    public async Task GetDevices_StoredDevices_ReturnsAllIncludingRevokedWithoutSideEffects()
    {
        var active = _harness.Devices.Add(Build.DeviceWithToken("a", FakeClock.Start));
        var revoked = _harness.Devices.Add(Build.DeviceWithToken("b", FakeClock.Start));
        revoked.Revoke(FakeClock.Start);

        var result = await _harness.GetDevices().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal([active, revoked], result.Value);
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }
}
