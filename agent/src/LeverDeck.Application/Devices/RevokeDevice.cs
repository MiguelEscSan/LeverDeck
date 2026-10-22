using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Devices;

/// <summary>
/// Command (admin scope): revokes a device so its token is rejected from now on. The transport closes its open
/// connection when it hears <see cref="DeviceRevoked"/>.
/// </summary>
public sealed class RevokeDevice(IDeviceRepository devices, IClock clock, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, DeviceId deviceId, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var device = await devices.GetByIdAsync(deviceId, cancellationToken).ConfigureAwait(false);
            if (device is null)
            {
                return UseCase.Fail(DomainErrorCode.DeviceNotFound, $"Device {deviceId} does not exist.");
            }

            device.Revoke(clock.UtcNow);
            await devices.SaveAsync(device, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new DeviceRevoked(device.Id), cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new DevicesChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
