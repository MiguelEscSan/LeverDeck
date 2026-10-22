using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Ports;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates the device or replaces the stored one with the same id.</summary>
    Task SaveAsync(Device device, CancellationToken cancellationToken);

    /// <summary>The device whose token hash matches, revoked or not. Adapters should compare in constant time.</summary>
    Task<Device?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken);
}
