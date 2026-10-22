using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Devices;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset _pairedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DeviceName _name = new("Pixel");
    private static readonly TokenHash _hash = new("hash-1");

    private static Device NewDevice() => Device.Pair(_name, _hash, _pairedAt);

    private static void AssertFails(DomainErrorCode expected, Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error!.Code);
    }

    // Pair / Restore

    [Fact]
    public void Pair_NewDevice_SeenWhenPairedAndNotRevoked()
    {
        var device = NewDevice();

        Assert.NotEqual(Guid.Empty, device.Id.Value);
        Assert.Equal(_name, device.Name);
        Assert.Equal(_hash, device.TokenHash);
        Assert.Equal(_pairedAt, device.PairedAt);
        Assert.Equal(_pairedAt, device.LastSeenAt);
        Assert.Null(device.RevokedAt);
        Assert.False(device.IsRevoked);
    }

    [Fact]
    public void Pair_NullArguments_ThrowArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Device.Pair(null!, _hash, _pairedAt));
        Assert.Throws<ArgumentNullException>(() => Device.Pair(_name, null!, _pairedAt));
    }

    [Fact]
    public void Restore_ValidState_KeepsState()
    {
        var id = DeviceId.New();
        var lastSeen = _pairedAt.AddHours(1);
        var revoked = _pairedAt.AddHours(2);

        var device = Device.Restore(id, _name, _hash, _pairedAt, lastSeen, revoked);

        Assert.Equal(id, device.Id);
        Assert.Equal(lastSeen, device.LastSeenAt);
        Assert.Equal(revoked, device.RevokedAt);
        Assert.True(device.IsRevoked);
    }

    [Fact]
    public void Restore_DefaultId_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Device.Restore(default, _name, _hash, _pairedAt, _pairedAt, null));
    }

    [Fact]
    public void Restore_LastSeenBeforePaired_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Device.Restore(DeviceId.New(), _name, _hash, _pairedAt, _pairedAt.AddTicks(-1), null));
    }

    [Fact]
    public void Restore_RevokedBeforePaired_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Device.Restore(DeviceId.New(), _name, _hash, _pairedAt, _pairedAt, _pairedAt.AddTicks(-1)));
    }

    [Fact]
    public void Restore_SameInstantInAnotherOffset_IsComparedAsTheSameMoment()
    {
        var sameMomentInMadrid = _pairedAt.ToOffset(TimeSpan.FromHours(2));

        var device = Device.Restore(DeviceId.New(), _name, _hash, _pairedAt, sameMomentInMadrid, sameMomentInMadrid);

        Assert.Equal(_pairedAt, device.LastSeenAt);
    }

    // Rename

    [Fact]
    public void Rename_NewName_ReplacesName()
    {
        var device = NewDevice();

        device.Rename(new DeviceName("Tablet"));

        Assert.Equal(new DeviceName("Tablet"), device.Name);
    }

    [Fact]
    public void Rename_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => NewDevice().Rename(null!));
    }

    // Revoke

    [Fact]
    public void Revoke_ActiveDevice_RecordsRevocation()
    {
        var device = NewDevice();
        var now = _pairedAt.AddDays(1);

        device.Revoke(now);

        Assert.True(device.IsRevoked);
        Assert.Equal(now, device.RevokedAt);
    }

    [Fact]
    public void Revoke_Twice_KeepsFirstRevocation()
    {
        var device = NewDevice();
        var first = _pairedAt.AddDays(1);

        device.Revoke(first);
        device.Revoke(first.AddDays(1));

        Assert.Equal(first, device.RevokedAt);
    }

    [Fact]
    public void Revoke_ClockBeforePairing_StillRevokesAtPairingTime()
    {
        var device = NewDevice();

        device.Revoke(_pairedAt.AddMinutes(-5));

        Assert.True(device.IsRevoked);
        Assert.Equal(_pairedAt, device.RevokedAt);
    }

    // Connect

    [Fact]
    public void Connect_ActiveDevice_UpdatesLastSeen()
    {
        var device = NewDevice();
        var now = _pairedAt.AddMinutes(30);

        var result = device.Connect(now);

        Assert.True(result.IsSuccess);
        Assert.Equal(now, device.LastSeenAt);
    }

    [Fact]
    public void Connect_EarlierThanLastSeen_DoesNotMoveLastSeenBackwards()
    {
        var device = NewDevice();
        var later = _pairedAt.AddHours(1);
        device.Connect(later);

        var result = device.Connect(_pairedAt.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(later, device.LastSeenAt);
    }

    [Fact]
    public void Connect_RevokedDevice_FailsWithDeviceRevokedAndKeepsLastSeen()
    {
        var device = NewDevice();
        device.Revoke(_pairedAt.AddMinutes(1));

        var result = device.Connect(_pairedAt.AddHours(1));

        AssertFails(DomainErrorCode.DeviceRevoked, result);
        Assert.Equal(_pairedAt, device.LastSeenAt);
    }

    // Matches

    [Fact]
    public void Matches_SameHash_IsTrue()
    {
        Assert.True(NewDevice().Matches(new TokenHash("hash-1")));
    }

    [Theory]
    [InlineData("hash-2")]
    [InlineData("hash-10")]
    [InlineData("HASH-1")]
    public void Matches_DifferentHash_IsFalse(string candidate)
    {
        Assert.False(NewDevice().Matches(new TokenHash(candidate)));
    }

    [Fact]
    public void Matches_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => NewDevice().Matches(null!));
    }
}
