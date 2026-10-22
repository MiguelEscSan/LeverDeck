namespace LeverDeck.Domain.Errors;

/// <summary>Guard helpers that throw <see cref="InvariantViolationException"/>.</summary>
internal static class Invariant
{
    public static void Require(bool condition, DomainErrorCode code, string message)
    {
        if (!condition)
        {
            throw new InvariantViolationException(code, message);
        }
    }

    public static void RequireDefined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum =>
        Require(Enum.IsDefined(value), DomainErrorCode.ValidationFailed, $"Unknown {name} '{value}'.");
}
