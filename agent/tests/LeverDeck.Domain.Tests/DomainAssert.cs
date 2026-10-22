using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests;

internal static class DomainAssert
{
    public static void Violates(DomainErrorCode expected, Action act)
    {
        var exception = Assert.ThrowsAny<DomainException>(act);
        Assert.Equal(expected, exception.Code);
    }
}
