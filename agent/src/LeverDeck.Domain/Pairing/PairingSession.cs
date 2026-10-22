using System.Security.Cryptography;
using System.Text;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Pairing;

/// <summary>
/// A single-use pairing code valid for <see cref="Lifetime"/>. Keeping only one active session at a time is the
/// job of the session store and the pairing use case, not of this entity.
/// </summary>
public sealed class PairingSession
{
    private PairingSession(PairingCode code, DateTimeOffset startedAt)
    {
        Code = code;
        StartedAt = startedAt;
        ExpiresAt = startedAt + Lifetime;
    }

    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(5);

    public PairingCode Code { get; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>The first instant at which the session is no longer valid.</summary>
    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    public static PairingSession Start(PairingCode code, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new PairingSession(code, now);
    }

    public bool IsActive(DateTimeOffset now) => RedeemedAt is null && now < ExpiresAt;

    /// <summary>
    /// Redeems the session with a candidate code. Checks run in this order, so the outcome for a used or expired
    /// session never depends on whether the candidate was right:
    /// <list type="number">
    /// <item>already redeemed → <see cref="DomainErrorCode.PairingCodeInvalid"/> (single use);</item>
    /// <item>expired (<paramref name="now"/> ≥ <see cref="ExpiresAt"/>) → <see cref="DomainErrorCode.PairingCodeExpired"/>;</item>
    /// <item>code mismatch (constant-time comparison) → <see cref="DomainErrorCode.PairingCodeInvalid"/>.</item>
    /// </list>
    /// A failure changes nothing, so a wrong guess does not burn the session; limiting attempts is a use-case concern.
    /// </summary>
    public Result Redeem(PairingCode candidate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var codeMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Code.Value),
            Encoding.ASCII.GetBytes(candidate.Value));

        if (RedeemedAt is not null)
        {
            return Failure(DomainErrorCode.PairingCodeInvalid, "Pairing code is invalid.");
        }

        if (now >= ExpiresAt)
        {
            return Failure(DomainErrorCode.PairingCodeExpired, "Pairing code has expired.");
        }

        if (!codeMatches)
        {
            return Failure(DomainErrorCode.PairingCodeInvalid, "Pairing code is invalid.");
        }

        RedeemedAt = now;
        return Result.Success();
    }

    private static Result Failure(DomainErrorCode code, string message) => Result.Failure(new DomainError(code, message));
}
