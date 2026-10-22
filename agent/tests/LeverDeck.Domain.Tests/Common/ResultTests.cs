using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly DomainError _notFound = new(DomainErrorCode.ProfileNotFound, "Profile not found.");

    [Fact]
    public void Success_IsSuccessWithoutError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_IsFailureWithError()
    {
        var result = Result.Failure(_notFound);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(_notFound, result.Error);
    }

    [Fact]
    public void Failure_NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void SuccessOfT_ExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FailureOfT_ExposesErrorAndRejectsValueAccess()
    {
        var result = Result.Failure<int>(_notFound);

        Assert.True(result.IsFailure);
        Assert.Equal(_notFound, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void FailureOfT_NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(null!));
    }
}
