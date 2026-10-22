using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Security;

/// <summary>
/// The caller of a use case. Built only through the factories, so a device caller always carries its id and the
/// other scopes never do.
/// </summary>
public sealed record Caller
{
    private Caller(CallerScope scope, DeviceId? deviceId)
    {
        Scope = scope;
        DeviceId = deviceId;
    }

    public static Caller Public { get; } = new(CallerScope.Public, null);

    public static Caller Admin { get; } = new(CallerScope.Admin, null);

    public CallerScope Scope { get; }

    public DeviceId? DeviceId { get; }

    public static Caller ForDevice(DeviceId deviceId) => new(CallerScope.Device, deviceId);
}
