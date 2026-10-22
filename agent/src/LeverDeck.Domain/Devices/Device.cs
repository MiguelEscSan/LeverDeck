using System.Security.Cryptography;
using System.Text;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Devices;

/// <summary>
/// Aggregate root for a paired phone or tablet. <see cref="LastSeenAt"/> and <see cref="RevokedAt"/> are never
/// before <see cref="PairedAt"/>. Once revoked, a device can never connect again.
/// </summary>
public sealed class Device
{
    private Device(
        DeviceId id,
        DeviceName name,
        TokenHash tokenHash,
        DateTimeOffset pairedAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        Name = name;
        TokenHash = tokenHash;
        PairedAt = pairedAt;
        LastSeenAt = lastSeenAt;
        RevokedAt = revokedAt;
    }

    public DeviceId Id { get; }

    public DeviceName Name { get; private set; }

    public TokenHash TokenHash { get; }

    public DateTimeOffset PairedAt { get; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Creates a newly paired device, seen for the first time at <paramref name="now"/>.</summary>
    public static Device Pair(DeviceName name, TokenHash tokenHash, DateTimeOffset now) =>
        Restore(DeviceId.New(), name, tokenHash, now, now, null);

    /// <summary>Rebuilds a device from stored state, validating every invariant.</summary>
    public static Device Restore(
        DeviceId id,
        DeviceName name,
        TokenHash tokenHash,
        DateTimeOffset pairedAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset? revokedAt)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tokenHash);
        Invariant.Require(id.Value != Guid.Empty, DomainErrorCode.ValidationFailed, "Device id must not be empty.");
        Invariant.Require(
            lastSeenAt >= pairedAt,
            DomainErrorCode.ValidationFailed,
            "A device cannot be seen before it was paired.");
        Invariant.Require(
            revokedAt is null || revokedAt.Value >= pairedAt,
            DomainErrorCode.ValidationFailed,
            "A device cannot be revoked before it was paired.");

        return new Device(id, name, tokenHash, pairedAt, lastSeenAt, revokedAt);
    }

    public void Rename(DeviceName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Revokes the device. Idempotent: a second call keeps the first revocation time. A clock earlier than the
    /// pairing never blocks a revocation; it is recorded at <see cref="PairedAt"/> instead.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = now < PairedAt ? PairedAt : now;
    }

    /// <summary>
    /// Records a connection. Fails with <see cref="DomainErrorCode.DeviceRevoked"/> for a revoked device;
    /// <see cref="LastSeenAt"/> never moves backwards.
    /// </summary>
    public Result Connect(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Result.Failure(new DomainError(DomainErrorCode.DeviceRevoked, $"Device {Id} has been revoked."));
        }

        if (now > LastSeenAt)
        {
            LastSeenAt = now;
        }

        return Result.Success();
    }

    /// <summary>
    /// Compares a presented token hash with this device's in constant time. It does not look at revocation;
    /// <see cref="Connect"/> does.
    /// </summary>
    public bool Matches(TokenHash candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(TokenHash.Value),
            Encoding.UTF8.GetBytes(candidate.Value));
    }
}
