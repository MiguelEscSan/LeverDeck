using System.Security.Cryptography;
using System.Text;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Pairing;

/// <summary>
/// A single-use pairing code valid for <see cref="Lifetime"/> that allows at most <see cref="MaxFailedAttempts"/>
/// wrong guesses. Keeping only one active session at a time is the job of the session store and the pairing use case,
/// not of this entity.
/// </summary>
public sealed class PairingSession
{
    private PairingSession(PairingCode code, DateTimeOffset startedAt)
    {
        Code = code;
        StartedAt = startedAt;
        ExpiresAt = startedAt + Lifetime;
    }

    public const int MaxFailedAttempts = 5;

    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(5);

    public PairingCode Code { get; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>The first instant at which the session is no longer valid.</summary>
    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>Wrong guesses so far; once it reaches <see cref="MaxFailedAttempts"/> the session is exhausted.</summary>
    public int FailedAttempts { get; private set; }

    public bool IsExhausted => FailedAttempts >= MaxFailedAttempts;

    public static PairingSession Start(PairingCode code, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new PairingSession(code, now);
    }

    public bool IsActive(DateTimeOffset now) => RedeemedAt is null && !IsExhausted && now < ExpiresAt;

    /// <summary>
    /// Redeems the session with a candidate code. Checks run in this order, so the outcome for a used or expired
    /// session never depends on whether the candidate was right:
    /// <list type="number">
    /// <item>already redeemed or exhausted → <see cref="DomainErrorCode.PairingCodeInvalid"/> (single use, attempt limit);</item>
    /// <item>expired (<paramref name="now"/> ≥ <see cref="ExpiresAt"/>) → <see cref="DomainErrorCode.PairingCodeExpired"/>;</item>
    /// <item>code mismatch (constant-time comparison) → <see cref="DomainErrorCode.PairingCodeInvalid"/>, counted in
    /// <see cref="FailedAttempts"/>.</item>
    /// </list>
    /// Only a mismatch on a live session changes state: the <see cref="MaxFailedAttempts"/>th one exhausts the session,
    /// so every later call fails with <see cref="DomainErrorCode.PairingCodeInvalid"/>, even with the right code.
    /// </summary>
    public Result Redeem(PairingCode candidate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var codeMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Code.Value),
            Encoding.ASCII.GetBytes(candidate.Value));

        if (RedeemedAt is not null || IsExhausted)
        {
            return Failure(DomainErrorCode.PairingCodeInvalid, "Pairing code is invalid.");
        }

        if (now >= ExpiresAt)
        {
            return Failure(DomainErrorCode.PairingCodeExpired, "Pairing code has expired.");
        }

        if (!codeMatches)
        {
            FailedAttempts++;
            return Failure(DomainErrorCode.PairingCodeInvalid, "Pairing code is invalid.");
        }

        RedeemedAt = now;
        return Result.Success();
    }

    private static Result Failure(DomainErrorCode code, string message) => Result.Failure(new DomainError(code, message));
}
