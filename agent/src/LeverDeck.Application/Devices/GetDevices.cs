using LeverDeck.Application.Common;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Devices;

/// <summary>Query (admin scope): every paired device, revoked ones included. Connection state is added in Phase 3.</summary>
public sealed class GetDevices(IDeviceRepository devices)
{
    public Task<Result<IReadOnlyList<Device>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
            Result.Success(await devices.ListAsync(cancellationToken).ConfigureAwait(false)));
}
