namespace LeverDeck.Domain.Errors;

/// <summary>
/// Thrown when constructing or mutating a domain object would break an invariant, so an invalid object never exists.
/// Expected business failures in use cases are returned as <see cref="Common.Result"/> instead.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(DomainErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainErrorCode Code { get; }

    public DomainError ToError() => new(Code, Message);
}

/// <summary>The single concrete invariant violation; the <see cref="DomainException.Code"/> tells which rule broke.</summary>
public sealed class InvariantViolationException(DomainErrorCode code, string message) : DomainException(code, message);
