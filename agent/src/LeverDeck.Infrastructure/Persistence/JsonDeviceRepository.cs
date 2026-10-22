using LeverDeck.Application.Devices.Ports;
using LeverDeck.Domain.Devices;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence.Records;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>Paired devices in <c>devices.json</c>, token hashes included. A missing file means no device yet.</summary>
public sealed class JsonDeviceRepository : IDeviceRepository, IDisposable
{
    public const string FileName = "devices.json";

    private readonly JsonDocumentFile<DevicesDocument> _file;

    public JsonDeviceRepository(LeverDeckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _file = new JsonDocumentFile<DevicesDocument>(
            Path.Combine(options.DataDirectory, FileName),
            PersistenceJsonContext.Default.DevicesDocument,
            document => document.SchemaVersion,
            DevicesDocument.CurrentVersion);
    }

    public Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken) =>
        _file.ReadAsync(
            current => Records(current).Find(record => record.Id == id.Value) is { } found
                ? DeviceRecordMapper.ToDomain(found)
                : null,
            cancellationToken);

    public Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken) =>
        _file.ReadAsync(
            current => (IReadOnlyList<Device>)[.. Records(current).Select(DeviceRecordMapper.ToDomain)],
            cancellationToken);

    public Task SaveAsync(Device device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        var record = DeviceRecordMapper.ToRecord(device);
        return _file.UpdateAsync(
            current =>
            {
                List<DeviceRecord> devices = [.. Records(current)];
                var index = devices.FindIndex(existing => existing.Id == record.Id);
                if (index >= 0)
                {
                    devices[index] = record;
                }
                else
                {
                    devices.Add(record);
                }

                return new DevicesDocument(DevicesDocument.CurrentVersion, devices);
            },
            cancellationToken);
    }

    /// <summary>
    /// Compares the candidate with every stored hash in constant time and never stops early, so the time taken does
    /// not reveal which device, if any, matched.
    /// </summary>
    public Task<Device?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        return _file.ReadAsync(
            current =>
            {
                Device? match = null;
                foreach (var device in Records(current).Select(DeviceRecordMapper.ToDomain))
                {
                    if (device.Matches(tokenHash) && match is null)
                    {
                        match = device;
                    }
                }

                return match;
            },
            cancellationToken);
    }

    private static List<DeviceRecord> Records(DevicesDocument? document) => document?.Devices ?? [];

    public void Dispose() => _file.Dispose();
}
