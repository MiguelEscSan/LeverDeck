using LeverDeck.Application.Settings;
using LeverDeck.Infrastructure.Apps;

namespace LeverDeck.Infrastructure.Tests.Apps;

public sealed class StartMenuStoreAppCatalogTests
{
    [Fact]
    public void Parse_ReadsEveryEntryOfAJsonArray()
    {
        const string json = """
            [
              { "Name": "Spotify", "AppID": "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" },
              { "Name": "Calculator", "AppID": "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" }
            ]
            """;

        Assert.Equal(
            [
                new StoreApp("Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"),
                new StoreApp("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
            ],
            StartMenuStoreAppCatalog.Parse(json));
    }

    [Fact]
    public void Parse_ReadsASingleEntryThatPowerShellWritesWithoutBrackets()
    {
        const string json = """{ "Name": "Spotify", "AppID": "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" }""";

        Assert.Equal(
            [new StoreApp("Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")],
            StartMenuStoreAppCatalog.Parse(json));
    }

    [Fact]
    public void Parse_SkipsEntriesWithoutAnAppId()
    {
        const string json = """[{ "Name": "Broken" }, { "Name": "Tool", "AppID": "Vendor.Tool_abc!Tool" }]""";

        Assert.Equal([new StoreApp("Tool", "Vendor.Tool_abc!Tool")], StartMenuStoreAppCatalog.Parse(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    public void Parse_UnreadableOutput_ContributesNoApps(string json)
    {
        Assert.Empty(StartMenuStoreAppCatalog.Parse(json));
    }
}
