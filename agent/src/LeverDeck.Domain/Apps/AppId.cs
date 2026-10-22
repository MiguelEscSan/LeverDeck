using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Apps;

/// <summary>Identifier of an application registered in the agent settings; the executable path lives there, never here.</summary>
public readonly record struct AppId
{
    public AppId(Guid value)
    {
        Invariant.Require(value != Guid.Empty, DomainErrorCode.ValidationFailed, "App id must not be empty.");
        Value = value;
    }

    public Guid Value { get; }

    public static AppId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
