using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Common;

/// <summary>
/// Shared shell of every use case: checks the caller's scope, then runs the body. A <see cref="DomainException"/>
/// thrown by the domain never escapes; it becomes a failed <see cref="Result"/> with the same code and message.
/// </summary>
internal static class UseCase
{
    public static async Task<Result> RunAsync(Caller caller, CallerScope required, Func<Task<Result>> body)
    {
        if (ScopeGuard.Check(caller, required) is { } denied)
        {
            return Result.Failure(denied);
        }

        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (DomainException exception)
        {
            return Result.Failure(exception.ToError());
        }
    }

    public static async Task<Result<T>> RunAsync<T>(Caller caller, CallerScope required, Func<Task<Result<T>>> body)
    {
        if (ScopeGuard.Check(caller, required) is { } denied)
        {
            return Result.Failure<T>(denied);
        }

        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (DomainException exception)
        {
            return Result.Failure<T>(exception.ToError());
        }
    }

    public static Result Fail(DomainErrorCode code, string message) => Result.Failure(new DomainError(code, message));

    public static Result<T> Fail<T>(DomainErrorCode code, string message) =>
        Result.Failure<T>(new DomainError(code, message));

    /// <summary>Carries the error of a failed result into a result of another type.</summary>
    public static Result<T> Fail<T>(Result failed) => Result.Failure<T>(failed.Error!);
}
