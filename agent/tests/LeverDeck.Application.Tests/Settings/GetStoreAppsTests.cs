using LeverDeck.Application.Security;
using LeverDeck.Application.Settings;

namespace LeverDeck.Application.Tests.Settings;

public sealed class GetStoreAppsTests
{
    private readonly Harness _harness = new();

    [Fact]
    public async Task ExecuteAsync_ListsPackagedAppsByNameAndDropsOtherTargets()
    {
        _harness.StoreApps.Apps.AddRange(
        [
            new StoreApp("Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"),
            new StoreApp("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
            new StoreApp("Desktop tool", @"C:\Tools\tool.exe"),
            new StoreApp("Injected", "x!y;calc"),
        ]);

        var result = await _harness.GetStoreApps().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal(["Calculator", "Spotify"], result.Value.Select(app => app.Name));
    }

    [Fact]
    public async Task ExecuteAsync_NamelessAppShowsItsAppUserModelId()
    {
        _harness.StoreApps.Apps.Add(new StoreApp("  ", "Vendor.Tool_abc123!Tool"));

        var result = await _harness.GetStoreApps().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal("Vendor.Tool_abc123!Tool", result.Value.Single().Name);
    }
}
