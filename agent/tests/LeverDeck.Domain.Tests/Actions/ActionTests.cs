using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Tests.Actions;

public sealed class HotkeyTests
{
    [Fact]
    public void Constructor_ModifiersAndKey_KeepsValuesAndRunsOnAgent()
    {
        var hotkey = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, VirtualKey.M);

        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Shift, hotkey.Modifiers);
        Assert.Equal(VirtualKey.M, hotkey.Key);
        Assert.Equal(ExecutionTarget.Agent, hotkey.Target);
    }

    [Fact]
    public void Constructor_AllModifiers_IsAccepted()
    {
        var all = KeyModifiers.Ctrl | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Win;

        Assert.Equal(all, new Hotkey(all, VirtualKey.F24).Modifiers);
    }

    [Fact]
    public void Constructor_NoModifiers_IsAccepted()
    {
        Assert.Equal(KeyModifiers.None, new Hotkey(KeyModifiers.None, VirtualKey.PrintScreen).Modifiers);
    }

    [Fact]
    public void Constructor_UndefinedKey_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new Hotkey(KeyModifiers.Ctrl, (VirtualKey)999));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(-1)]
    public void Constructor_UndefinedModifierBits_ThrowsValidationFailed(int modifiers)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new Hotkey((KeyModifiers)modifiers, VirtualKey.A));
    }

    [Fact]
    public void Equality_SameModifiersAndKey_IsEqual()
    {
        Assert.Equal(new Hotkey(KeyModifiers.Alt, VirtualKey.Tab), new Hotkey(KeyModifiers.Alt, VirtualKey.Tab));
    }
}

public sealed class MediaKeyTests
{
    [Theory]
    [InlineData(MediaKeyKind.PlayPause)]
    [InlineData(MediaKeyKind.Mute)]
    [InlineData(MediaKeyKind.VolumeUp)]
    public void Constructor_DefinedKey_KeepsKeyAndRunsOnAgent(MediaKeyKind key)
    {
        var media = new MediaKey(key);

        Assert.Equal(key, media.Key);
        Assert.Equal(ExecutionTarget.Agent, media.Target);
    }

    [Fact]
    public void Constructor_UndefinedKey_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new MediaKey((MediaKeyKind)42));
    }
}

public sealed class LaunchAppTests
{
    [Fact]
    public void Constructor_AppId_KeepsIdAndRunsOnAgent()
    {
        var app = AppId.New();

        var launch = new LaunchApp(app);

        Assert.Equal(app, launch.App);
        Assert.Equal(ExecutionTarget.Agent, launch.Target);
    }

    [Fact]
    public void Constructor_DefaultAppId_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new LaunchApp(default));
    }
}

public sealed class OpenUrlTests
{
    private const string Prefix = "https://example.com/";

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:8080/path?q=1")]
    public void Constructor_HttpOrHttpsUrl_KeepsUrlAndRunsOnAgent(string url)
    {
        var open = new OpenUrl(new Uri(url));

        Assert.Equal(new Uri(url), open.Url);
        Assert.Equal(ExecutionTarget.Agent, open.Target);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@example.com")]
    public void Constructor_OtherScheme_ThrowsValidationFailed(string url)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new OpenUrl(new Uri(url)));
    }

    [Fact]
    public void Constructor_RelativeUrl_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new OpenUrl(new Uri("/docs/page", UriKind.Relative)));
    }

    [Fact]
    public void Constructor_MaxLength_IsAccepted()
    {
        var url = new Uri(Prefix + new string('a', OpenUrl.MaxLength - Prefix.Length));

        Assert.Equal(OpenUrl.MaxLength, new OpenUrl(url).Url.AbsoluteUri.Length);
    }

    [Fact]
    public void Constructor_LongerThanMaxLength_ThrowsValidationFailed()
    {
        var url = new Uri(Prefix + new string('a', OpenUrl.MaxLength - Prefix.Length + 1));

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new OpenUrl(url));
    }

    [Fact]
    public void Constructor_NullUrl_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenUrl(null!));
    }
}

public sealed class ObsSceneTests
{
    [Fact]
    public void Constructor_SurroundingWhitespace_IsTrimmedAndRunsOnAgent()
    {
        var scene = new ObsScene("  Just Chatting  ");

        Assert.Equal("Just Chatting", scene.SceneName);
        Assert.Equal(ExecutionTarget.Agent, scene.Target);
    }

    [Fact]
    public void Constructor_OneChar_IsAccepted()
    {
        Assert.Equal("x", new ObsScene("x").SceneName);
    }

    [Fact]
    public void Constructor_HundredChars_IsAccepted()
    {
        var name = new string('x', ObsScene.MaxLength);

        Assert.Equal(name, new ObsScene(name).SceneName);
    }

    [Fact]
    public void Constructor_HundredOneChars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsScene(new string('x', ObsScene.MaxLength + 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationFailed(string? name)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ObsScene(name!));
    }
}

public sealed class LaunchGameTests
{
    [Theory]
    [InlineData(GameStore.Steam, "570")]
    [InlineData(GameStore.Steam, "1234567890")]
    [InlineData(GameStore.Epic, "Fortnite")]
    [InlineData(GameStore.Epic, "a1B2c3")]
    public void Constructor_ValidId_KeepsValuesAndRunsOnAgent(GameStore store, string gameId)
    {
        var game = new LaunchGame(store, gameId);

        Assert.Equal(store, game.Store);
        Assert.Equal(gameId, game.GameId);
        Assert.Equal(ExecutionTarget.Agent, game.Target);
    }

    [Fact]
    public void Constructor_SteamElevenDigits_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new LaunchGame(GameStore.Steam, "12345678901"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("57a")]
    [InlineData("-570")]
    [InlineData(" 570")]
    [InlineData("５７０")]
    public void Constructor_SteamNotDigits_ThrowsValidationFailed(string? gameId)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new LaunchGame(GameStore.Steam, gameId!));
    }

    [Fact]
    public void Constructor_EpicSixtyFourChars_IsAccepted()
    {
        var gameId = new string('a', LaunchGame.MaxEpicIdLength);

        Assert.Equal(gameId, new LaunchGame(GameStore.Epic, gameId).GameId);
    }

    [Fact]
    public void Constructor_EpicSixtyFiveChars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new LaunchGame(GameStore.Epic, new string('a', LaunchGame.MaxEpicIdLength + 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("fort-nite")]
    [InlineData("fort nite")]
    [InlineData("forté")]
    [InlineData("../evil")]
    public void Constructor_EpicNotAlphanumeric_ThrowsValidationFailed(string? gameId)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new LaunchGame(GameStore.Epic, gameId!));
    }

    [Fact]
    public void Constructor_UndefinedStore_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new LaunchGame((GameStore)9, "570"));
    }
}

public sealed class ChangePageTests
{
    [Fact]
    public void Constructor_Next_KeepsDestinationAndRunsOnClient()
    {
        var change = new ChangePage(PageTarget.Next);

        Assert.Equal(PageTarget.Next, change.Destination);
        Assert.Equal(PageTargetKind.Next, change.Destination.Kind);
        Assert.Null(change.Destination.Index);
        Assert.Equal(ExecutionTarget.Client, change.Target);
    }

    [Fact]
    public void Previous_HasPreviousKindAndNoIndex()
    {
        Assert.Equal(PageTargetKind.Previous, PageTarget.Previous.Kind);
        Assert.Null(PageTarget.Previous.Index);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void At_NonNegativeIndex_KeepsIndex(int index)
    {
        var target = PageTarget.At(index);

        Assert.Equal(PageTargetKind.Specific, target.Kind);
        Assert.Equal(index, target.Index);
        Assert.Equal(PageTarget.At(index), target);
    }

    [Fact]
    public void At_NegativeIndex_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => PageTarget.At(-1));
    }

    [Fact]
    public void Constructor_NullDestination_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ChangePage(null!));
    }
}

public sealed class SwitchProfileTests
{
    [Fact]
    public void Constructor_ProfileId_KeepsIdAndRunsOnClient()
    {
        var profile = ProfileId.New();

        var switchProfile = new SwitchProfile(profile);

        Assert.Equal(profile, switchProfile.Profile);
        Assert.Equal(ExecutionTarget.Client, switchProfile.Target);
    }

    [Fact]
    public void Constructor_DefaultProfileId_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new SwitchProfile(default));
    }
}

public sealed class ActionCatalogTests
{
    [Fact]
    public void All_ListsEveryWhitelistedActionOnce()
    {
        Type[] expected =
        [
            typeof(Hotkey),
            typeof(MediaKey),
            typeof(LaunchApp),
            typeof(OpenUrl),
            typeof(ObsScene),
            typeof(LaunchGame),
            typeof(ChangePage),
            typeof(SwitchProfile),
            typeof(PlaySound),
        ];

        Assert.Equal(expected, ActionCatalog.All);
    }
}
