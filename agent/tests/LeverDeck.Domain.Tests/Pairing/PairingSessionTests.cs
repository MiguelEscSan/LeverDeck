using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Domain.Tests.Pairing;

public sealed class PairingSessionTests
{
    private static readonly DateTimeOffset _startedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly PairingCode _code = new("123456");
    private static readonly PairingCode _wrongCode = new("654321");

    private static PairingSession NewSession() => PairingSession.Start(_code, _startedAt);

    private static DateTimeOffset JustBeforeExpiry => _startedAt.AddMinutes(5).AddMilliseconds(-1);

    private static DateTimeOffset AtExpiry => _startedAt.AddMinutes(5);

    private static void AssertFails(DomainErrorCode expected, Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error!.Code);
    }

    // Start

    [Fact]
    public void Lifetime_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), PairingSession.Lifetime);
    }

    [Fact]
    public void Start_NewSession_ExpiresAfterLifetimeAndIsNotRedeemed()
    {
        var session = NewSession();

        Assert.Equal(_code, session.Code);
        Assert.Equal(_startedAt, session.StartedAt);
        Assert.Equal(_startedAt + PairingSession.Lifetime, session.ExpiresAt);
        Assert.Null(session.RedeemedAt);
    }

    [Fact]
    public void Start_NullCode_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PairingSession.Start(null!, _startedAt));
    }

    // IsActive

    [Fact]
    public void IsActive_JustBeforeExpiry_IsTrue()
    {
        Assert.True(NewSession().IsActive(JustBeforeExpiry));
    }

    [Fact]
    public void IsActive_AtExpiry_IsFalse()
    {
        Assert.False(NewSession().IsActive(AtExpiry));
    }

    [Fact]
    public void IsActive_AfterRedeemed_IsFalse()
    {
        var session = NewSession();
        session.Redeem(_code, _startedAt.AddMinutes(1));

        Assert.False(session.IsActive(_startedAt.AddMinutes(2)));
    }

    [Fact]
    public void IsActive_SameInstantInAnotherOffset_IsComparedAsTheSameMoment()
    {
        var expiryInMadrid = AtExpiry.ToOffset(TimeSpan.FromHours(2));

        Assert.False(NewSession().IsActive(expiryInMadrid));
    }

    // Redeem

    [Fact]
    public void Redeem_RightCodeJustBeforeExpiry_SucceedsAndRecordsRedemption()
    {
        var session = NewSession();

        var result = session.Redeem(new PairingCode("123456"), JustBeforeExpiry);

        Assert.True(result.IsSuccess);
        Assert.Equal(JustBeforeExpiry, session.RedeemedAt);
    }

    [Fact]
    public void Redeem_RightCodeAtExpiry_FailsWithExpired()
    {
        var session = NewSession();

        AssertFails(DomainErrorCode.PairingCodeExpired, session.Redeem(_code, AtExpiry));
        Assert.Null(session.RedeemedAt);
    }

    [Fact]
    public void Redeem_WrongCode_FailsWithInvalidAndKeepsSessionActive()
    {
        var session = NewSession();

        AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_wrongCode, _startedAt.AddMinutes(1)));
        Assert.Null(session.RedeemedAt);
        Assert.True(session.IsActive(_startedAt.AddMinutes(1)));
    }

    [Fact]
    public void Redeem_AlreadyRedeemed_FailsWithInvalidAndKeepsFirstRedemption()
    {
        var session = NewSession();
        var first = _startedAt.AddMinutes(1);
        session.Redeem(_code, first);

        AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_code, _startedAt.AddMinutes(2)));
        Assert.Equal(first, session.RedeemedAt);
    }

    [Fact]
    public void Redeem_AlreadyRedeemedAndExpired_FailsWithInvalid()
    {
        var session = NewSession();
        session.Redeem(_code, _startedAt.AddMinutes(1));

        AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_code, AtExpiry.AddMinutes(1)));
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("654321")]
    public void Redeem_ExpiredSession_FailsWithExpiredWhateverTheCode(string candidate)
    {
        var session = NewSession();

        AssertFails(DomainErrorCode.PairingCodeExpired, session.Redeem(new PairingCode(candidate), AtExpiry));
    }

    // Attempt limit

    [Fact]
    public void MaxFailedAttempts_IsFive()
    {
        Assert.Equal(5, PairingSession.MaxFailedAttempts);
    }

    [Fact]
    public void Redeem_WrongCode_IncrementsFailedAttempts()
    {
        var session = NewSession();

        session.Redeem(_wrongCode, _startedAt.AddMinutes(1));
        session.Redeem(_wrongCode, _startedAt.AddMinutes(1));

        Assert.Equal(2, session.FailedAttempts);
    }

    [Fact]
    public void Redeem_RightCodeAfterFewerThanMaxWrongGuesses_Succeeds()
    {
        var session = NewSession();
        var now = _startedAt.AddMinutes(1);
        for (var attempt = 1; attempt < PairingSession.MaxFailedAttempts; attempt++)
        {
            session.Redeem(_wrongCode, now);
        }

        Assert.True(session.Redeem(_code, now).IsSuccess);
    }

    [Fact]
    public void Redeem_RightCodeAfterMaxWrongGuesses_FailsWithInvalid()
    {
        var session = NewSession();
        var now = _startedAt.AddMinutes(1);
        for (var attempt = 0; attempt < PairingSession.MaxFailedAttempts; attempt++)
        {
            AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_wrongCode, now));
        }

        AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_code, now));
        Assert.Null(session.RedeemedAt);
    }

    [Fact]
    public void IsActive_AfterMaxWrongGuesses_IsFalse()
    {
        var session = NewSession();
        var now = _startedAt.AddMinutes(1);
        for (var attempt = 0; attempt < PairingSession.MaxFailedAttempts; attempt++)
        {
            session.Redeem(_wrongCode, now);
        }

        Assert.False(session.IsActive(now));
    }

    [Fact]
    public void Redeem_ExhaustedAndExpired_FailsWithInvalid()
    {
        var session = NewSession();
        for (var attempt = 0; attempt < PairingSession.MaxFailedAttempts; attempt++)
        {
            session.Redeem(_wrongCode, _startedAt.AddMinutes(1));
        }

        AssertFails(DomainErrorCode.PairingCodeInvalid, session.Redeem(_code, AtExpiry.AddMinutes(1)));
    }

    [Fact]
    public void Redeem_ExhaustedSession_StopsCountingAttempts()
    {
        var session = NewSession();
        var now = _startedAt.AddMinutes(1);
        for (var attempt = 0; attempt < PairingSession.MaxFailedAttempts + 3; attempt++)
        {
            session.Redeem(_wrongCode, now);
        }

        Assert.Equal(PairingSession.MaxFailedAttempts, session.FailedAttempts);
    }

    [Fact]
    public void Redeem_WrongCodeOnExpiredSession_DoesNotCountAnAttempt()
    {
        var session = NewSession();

        session.Redeem(_wrongCode, AtExpiry);

        Assert.Equal(0, session.FailedAttempts);
    }

    [Fact]
    public void Redeem_WrongCodeOnRedeemedSession_DoesNotCountAnAttempt()
    {
        var session = NewSession();
        session.Redeem(_code, _startedAt.AddMinutes(1));

        session.Redeem(_wrongCode, _startedAt.AddMinutes(2));

        Assert.Equal(0, session.FailedAttempts);
    }

    [Fact]
    public void Redeem_NullCandidate_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => NewSession().Redeem(null!, _startedAt));
    }
}
