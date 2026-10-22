using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Security;

/// <summary>
/// The single scope rule: a caller may run a use case whose scope is at or below its own
/// (<see cref="CallerScope.Public"/> &lt; <see cref="CallerScope.Device"/> &lt; <see cref="CallerScope.Admin"/>).
/// A public caller asking for more gets <see cref="DomainErrorCode.Unauthorized"/> (it must authenticate first); an
/// authenticated caller asking for more gets <see cref="DomainErrorCode.Forbidden"/> (no credential will help).
/// </summary>
internal static class ScopeGuard
{
    public static DomainError? Check(Caller caller, CallerScope required)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (caller.Scope >= required)
        {
            return null;
        }

        return caller.Scope == CallerScope.Public
            ? new DomainError(DomainErrorCode.Unauthorized, "Authentication is required.")
            : new DomainError(DomainErrorCode.Forbidden, $"This operation requires the {required} scope.");
    }
}
