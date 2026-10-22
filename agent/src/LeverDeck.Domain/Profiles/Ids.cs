using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

public readonly record struct ProfileId
{
    public ProfileId(Guid value)
    {
        Invariant.Require(value != Guid.Empty, DomainErrorCode.ValidationFailed, "Profile id must not be empty.");
        Value = value;
    }

    public Guid Value { get; }

    public static ProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct ButtonId
{
    public ButtonId(Guid value)
    {
        Invariant.Require(value != Guid.Empty, DomainErrorCode.ValidationFailed, "Button id must not be empty.");
        Value = value;
    }

    public Guid Value { get; }

    public static ButtonId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
