using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Devices;

public readonly record struct DeviceId
{
    public DeviceId(Guid value)
    {
        Invariant.Require(value != Guid.Empty, DomainErrorCode.ValidationFailed, "Device id must not be empty.");
        Value = value;
    }

    public Guid Value { get; }

    public static DeviceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
