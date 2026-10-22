using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Tests.Apps;

public sealed class AppIdTests
{
    [Fact]
    public void New_IsUniqueAndNotEmpty()
    {
        var first = AppId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, AppId.New());
    }

    [Fact]
    public void Constructor_EmptyGuid_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new AppId(Guid.Empty));
    }

    [Fact]
    public void Equality_SameGuid_IsEqual()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new AppId(guid), new AppId(guid));
        Assert.Equal(guid.ToString(), new AppId(guid).ToString());
    }
}
