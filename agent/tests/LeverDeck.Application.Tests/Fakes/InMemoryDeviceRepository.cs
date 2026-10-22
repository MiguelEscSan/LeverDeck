using LeverDeck.Application.Devices.Ports;
using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly List<Device> _devices = [];

    public int Writes { get; private set; }

    public IReadOnlyList<Device> All => _devices;

    /// <summary>Seeds a device without counting it as a write.</summary>
    public Device Add(Device device)
    {
        _devices.Add(device);
        return device;
    }

    public Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken) =>
        Task.FromResult(_devices.Find(device => device.Id == id));

    public Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Device>>([.. _devices]);

    public Task SaveAsync(Device device, CancellationToken cancellationToken)
    {
        Writes++;
        var index = _devices.FindIndex(existing => existing.Id == device.Id);
        if (index >= 0)
        {
            _devices[index] = device;
        }
        else
        {
            _devices.Add(device);
        }

        return Task.CompletedTask;
    }

    public Task<Device?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_devices.Find(device => device.Matches(tokenHash)));
}
