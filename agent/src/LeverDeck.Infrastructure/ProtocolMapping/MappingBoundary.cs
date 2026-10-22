using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>
/// Turns a DTO-to-domain mapping into a <see cref="Result{T}"/>. Whatever a malformed DTO makes the domain or the
/// mapping throw becomes a failure that reaches the wire as <c>validationFailed</c>; nothing escapes.
/// </summary>
internal static class MappingBoundary
{
    public static Result<T> Run<T>(Func<T> map)
    {
        try
        {
            return Result.Success(map());
        }
        catch (DomainException exception)
        {
            return Result.Failure<T>(exception.ToError());
        }
        catch (ArgumentException exception)
        {
            return Invalid<T>(exception.Message);
        }
        catch (FormatException exception)
        {
            return Invalid<T>(exception.Message);
        }
        catch (OverflowException exception)
        {
            return Invalid<T>(exception.Message);
        }
    }

    /// <summary>Rejects a null reference that the wire contract marks as required.</summary>
    public static T Required<T>(T? value, string name)
        where T : class =>
        value ?? throw new ArgumentNullException(name, $"'{name}' is required.");

    private static Result<T> Invalid<T>(string message) =>
        Result.Failure<T>(new DomainError(DomainErrorCode.ValidationFailed, message));
}
