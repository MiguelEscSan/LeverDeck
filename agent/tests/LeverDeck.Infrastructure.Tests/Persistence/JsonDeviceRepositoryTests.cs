using LeverDeck.Domain.Devices;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Tests.Persistence;

public sealed class JsonDeviceRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset PairedAt = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private JsonDeviceRepository NewRepository() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    private string FilePath => _directory.File("devices.json");

    private static Device NewDevice(string name, string hash) => Device.Pair(new DeviceName(name), new TokenHash(hash), PairedAt);

    [Fact]
    public async Task List_OnFirstRun_IsEmptyAndWritesNothing()
    {
        var devices = await NewRepository().ListAsync(CancellationToken.None);

        Assert.Empty(devices);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsEveryField()
    {
        var device = NewDevice("Pixel", "aa11");
        device.Connect(PairedAt.AddMinutes(5));
        device.Revoke(PairedAt.AddHours(1));

        await NewRepository().SaveAsync(device, CancellationToken.None);
        var loaded = await NewRepository().GetByIdAsync(device.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(device.Name, loaded.Name);
        Assert.Equal(device.TokenHash, loaded.TokenHash);
        Assert.Equal(device.PairedAt, loaded.PairedAt);
        Assert.Equal(device.LastSeenAt, loaded.LastSeenAt);
        Assert.Equal(device.RevokedAt, loaded.RevokedAt);
    }

    [Fact]
    public async Task Save_StoresTheTokenHashOnDisk()
    {
        await NewRepository().SaveAsync(NewDevice("Pixel", "0123abcd"), CancellationToken.None);

        Assert.Contains("\"tokenHash\": \"0123abcd\"", await File.ReadAllTextAsync(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_ReplacesTheDeviceWithTheSameId()
    {
        var repository = NewRepository();
        var device = NewDevice("Pixel", "aa11");
        await repository.SaveAsync(device, CancellationToken.None);

        device.Rename(new DeviceName("Tablet"));
        await repository.SaveAsync(device, CancellationToken.None);

        var stored = Assert.Single(await NewRepository().ListAsync(CancellationToken.None));
        Assert.Equal("Tablet", stored.Name.Value);
    }

    [Fact]
    public async Task FindByTokenHash_ReturnsTheMatchingDeviceOrNull()
    {
        var repository = NewRepository();
        var pixel = NewDevice("Pixel", "aa11");
        await repository.SaveAsync(pixel, CancellationToken.None);
        await repository.SaveAsync(NewDevice("iPad", "bb22"), CancellationToken.None);

        var found = await repository.FindByTokenHashAsync(new TokenHash("aa11"), CancellationToken.None);
        var missing = await repository.FindByTokenHashAsync(new TokenHash("cc33"), CancellationToken.None);

        Assert.Equal(pixel.Id, found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task CorruptFile_FailsLoudlyAndIsLeftUntouched()
    {
        await File.WriteAllTextAsync(FilePath, """{ "schemaVersion": 1, "devices": [ { "id": 3 } ] }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => NewRepository().ListAsync(CancellationToken.None));
        Assert.Contains("\"id\": 3", await File.ReadAllTextAsync(FilePath), StringComparison.Ordinal);
    }
}
