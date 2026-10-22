using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Devices;

/// <summary>
/// Command (public scope): authenticates a connection with a device token and records that the device was seen.
/// Documented exception to "commands return only an ack": it returns the <see cref="DeviceId"/>, because the
/// transport must know which device the connection belongs to.
/// </summary>
public sealed class ConnectDevice(IDeviceRepository devices, ITokenHasher hasher, IClock clock, IEventPublisher events)
{
    public Task<Result<DeviceId>> ExecuteAsync(
        Caller caller,
        string rawToken,
        int protocolVersion,
        CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Public, async () =>
        {
            if (protocolVersion != ApplicationInfo.ProtocolVersion)
            {
                return UseCase.Fail<DeviceId>(
                    DomainErrorCode.ProtocolMismatch,
                    $"Protocol version {protocolVersion} is not supported; the agent speaks {ApplicationInfo.ProtocolVersion}.");
            }

            var device = string.IsNullOrWhiteSpace(rawToken)
                ? null
                : await devices.FindByTokenHashAsync(hasher.Hash(rawToken), cancellationToken).ConfigureAwait(false);
            if (device is null)
            {
                return UseCase.Fail<DeviceId>(DomainErrorCode.Unauthorized, "Unknown device token.");
            }

            var connected = device.Connect(clock.UtcNow);
            if (connected.IsFailure)
            {
                return UseCase.Fail<DeviceId>(connected);
            }

            await devices.SaveAsync(device, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new DevicesChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success(device.Id);
        });
}
