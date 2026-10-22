using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Common;

/// <summary>Outcome of an operation that can fail for an expected business reason.</summary>
public class Result
{
    protected Result(DomainError? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public DomainError? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}

/// <summary>Outcome carrying a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, DomainError? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>The value of a successful result; reading it on a failure is a programming error.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value ({Error!.Code}).");
}
