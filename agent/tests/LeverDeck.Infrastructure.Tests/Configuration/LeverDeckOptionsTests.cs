using LeverDeck.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace LeverDeck.Infrastructure.Tests.Configuration;

public sealed class LeverDeckOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    [Fact]
    public void Defaults_UseTheRoamingAppDataFolderAndAdvertise()
    {
        var options = LeverDeckOptions.FromConfiguration(Config());

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Equal(Path.Combine(appData, "LeverDeck"), options.DataDirectory);
        Assert.True(options.MdnsEnabled);
    }

    [Fact]
    public void Defaults_ServeHttpAndManageTheDesktop()
    {
        var options = LeverDeckOptions.FromConfiguration(Config());

        Assert.False(options.HttpsEnabled);
        Assert.True(options.DesktopEnabled);
    }

    [Fact]
    public void Configuration_OverridesTheDataDirectoryAndTheAdvertiser()
    {
        var options = LeverDeckOptions.FromConfiguration(
            Config(("LeverDeck:DataDirectory", @"C:\Temp\ld"), ("LeverDeck:Mdns:Enabled", "false")));

        Assert.Equal(@"C:\Temp\ld", options.DataDirectory);
        Assert.False(options.MdnsEnabled);
    }

    [Fact]
    public void MdnsHostName_IsReadTrimmedAndAbsentWhenBlank()
    {
        Assert.Equal("studio", LeverDeckOptions.FromConfiguration(Config(("LeverDeck:Mdns:HostName", " studio "))).MdnsHostName);
        Assert.Null(LeverDeckOptions.FromConfiguration(Config(("LeverDeck:Mdns:HostName", "  "))).MdnsHostName);
        Assert.Null(LeverDeckOptions.FromConfiguration(Config()).MdnsHostName);
    }

    [Fact]
    public void InvalidFlag_FailsLoudly()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => LeverDeckOptions.FromConfiguration(Config(("LeverDeck:Mdns:Enabled", "nope"))));

        Assert.Contains("LeverDeck:Mdns:Enabled", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeDataDirectory_FailsLoudly()
    {
        Assert.Throws<InvalidOperationException>(
            () => LeverDeckOptions.FromConfiguration(Config(("LeverDeck:DataDirectory", "relative"))));
    }

    [Fact]
    public void ConnectTimeout_DefaultsToTenSecondsAndCanBeOverridden()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), LeverDeckOptions.FromConfiguration(Config()).ConnectTimeout);
        Assert.Equal(
            TimeSpan.FromMilliseconds(500),
            LeverDeckOptions.FromConfiguration(Config(("LeverDeck:Transport:ConnectTimeout", "00:00:00.5"))).ConnectTimeout);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("00:00:00")]
    [InlineData("-00:00:05")]
    public void InvalidConnectTimeout_FailsLoudly(string raw)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => LeverDeckOptions.FromConfiguration(Config(("LeverDeck:Transport:ConnectTimeout", raw))));

        Assert.Contains("LeverDeck:Transport:ConnectTimeout", error.Message, StringComparison.Ordinal);
    }
}
