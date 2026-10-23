using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Domain.Tests.Settings;

public sealed class RegisteredAppTests
{
    private const string Discord = @"C:\Apps\Discord\Discord.exe";

    private static string PathOfLength(int length)
    {
        const string prefix = @"C:\";
        const string suffix = ".exe";
        return prefix + new string('a', length - prefix.Length - suffix.Length) + suffix;
    }

    [Fact]
    public void Constructor_ValidValues_KeepsTrimmedState()
    {
        var id = AppId.New();

        var app = new RegisteredApp(id, "  Discord  ", $"  {Discord}  ");

        Assert.Equal(id, app.Id);
        Assert.Equal("Discord", app.DisplayName);
        Assert.Equal(Discord, app.ExecutablePath);
    }

    [Fact]
    public void Constructor_DefaultId_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new RegisteredApp(default, "Discord", Discord));
    }

    [Fact]
    public void Constructor_DisplayName40Chars_IsAccepted()
    {
        var name = new string('x', 40);

        Assert.Equal(name, new RegisteredApp(AppId.New(), name, Discord).DisplayName);
    }

    [Fact]
    public void Constructor_DisplayName41Chars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new RegisteredApp(AppId.New(), new string('x', 41), Discord));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyDisplayName_ThrowsValidationFailed(string? name)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new RegisteredApp(AppId.New(), name!, Discord));
    }

    [Fact]
    public void Constructor_Path260Chars_IsAccepted()
    {
        var path = PathOfLength(260);

        Assert.Equal(path, new RegisteredApp(AppId.New(), "App", path).ExecutablePath);
    }

    [Fact]
    public void Constructor_Path261Chars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new RegisteredApp(AppId.New(), "App", PathOfLength(261)));
    }

    [Theory]
    [InlineData(@"\\server\share\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe")]
    public void Constructor_FullyQualifiedPath_IsAccepted(string path)
    {
        Assert.Equal(path, new RegisteredApp(AppId.New(), "App", path).ExecutablePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("app.exe")]
    [InlineData(@"Apps\app.exe")]
    [InlineData(@"..\app.exe")]
    [InlineData(@"C:app.exe")]
    [InlineData(@"\Apps\app.exe")]
    public void Constructor_EmptyOrRelativePath_ThrowsValidationFailed(string? path)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new RegisteredApp(AppId.New(), "App", path!));
    }

    [Theory]
    [InlineData("\"C:\\Apps\\app.exe\"")]
    [InlineData("\"C:\\Apps\\app.exe\" --evil")]
    [InlineData("C:\\Apps\\app.exe\" --evil")]
    public void Constructor_PathWithQuotes_ThrowsValidationFailed(string path)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new RegisteredApp(AppId.New(), "App", path));
    }

    private const string Spotify = @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";

    [Fact]
    public void Constructor_StoreAppTarget_IsAcceptedAndMarkedAsStoreApp()
    {
        var app = new RegisteredApp(AppId.New(), "Spotify", Spotify);

        Assert.True(app.IsStoreApp);
        Assert.Equal(Spotify, app.ExecutablePath);
        Assert.False(new RegisteredApp(AppId.New(), "Discord", Discord).IsStoreApp);
    }

    [Theory]
    [InlineData(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0")]
    [InlineData(@"shell:AppsFolder\Spot ify!Spotify")]
    [InlineData(@"shell:AppsFolder\Spotify!Spotify;calc")]
    [InlineData(@"shell:AppsFolder\!Spotify")]
    [InlineData(@"shell:AppsFolder\")]
    [InlineData(@"shell:Other\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    public void Constructor_InvalidStoreAppTarget_ThrowsValidationFailed(string path)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new RegisteredApp(AppId.New(), "App", path));
    }
}
