using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Devices.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Devices;

/// <summary>Command (admin scope): renames a paired device.</summary>
public sealed class RenameDevice(IDeviceRepository devices, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, DeviceId deviceId, string name, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var device = await devices.GetByIdAsync(deviceId, cancellationToken).ConfigureAwait(false);
            if (device is null)
            {
                return UseCase.Fail(DomainErrorCode.DeviceNotFound, $"Device {deviceId} does not exist.");
            }

            device.Rename(new DeviceName(name));
            await devices.SaveAsync(device, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new DevicesChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
