using LeverDeck.Infrastructure.Discovery;

namespace LeverDeck.Infrastructure.Tests.Discovery;

public sealed class AgentHostNameTests
{
    private static string NoSuffix() => throw new InvalidOperationException("The machine name was enough.");

    [Theory]
    [InlineData("DESKTOP-Miguel Ñ", "leverdeck-desktop-miguel-n")]
    [InlineData("Office_PC", "leverdeck-office-pc")]
    [InlineData("--Studio  2--", "leverdeck-studio-2")]
    [InlineData("Équipe.Salón", "leverdeck-equipe-salon")]
    public void NamesThePcAfterItsMachineName(string machineName, string label)
    {
        var name = AgentHostName.FromMachineName(machineName, NoSuffix);

        Assert.Equal(label, name.Label);
        Assert.Equal($"{label}.local", name.Fqdn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("東京")]
    public void UsesTheRandomSuffixWhenNothingOfTheMachineNameIsLeft(string machineName)
    {
        var name = AgentHostName.FromMachineName(machineName, () => "k3x9qa");

        Assert.Equal("leverdeck-k3x9qa", name.Label);
    }

    [Fact]
    public void CutsTheLabelTo63CharactersWithoutATrailingDash()
    {
        var machineName = new string('a', 52) + "-bcdefghij";

        var name = AgentHostName.FromMachineName(machineName, NoSuffix);

        Assert.Equal(63, ("leverdeck-" + new string('a', 52) + "-").Length);
        Assert.Equal("leverdeck-" + new string('a', 52), name.Label);
    }

    [Theory]
    [InlineData("leverdeck-office", "leverdeck-office")]
    [InlineData("Leverdeck-Office.LOCAL", "leverdeck-office")]
    [InlineData("studio", "studio")]
    [InlineData(" studio.local ", "studio")]
    public void ParsesALabelOrAFullName(string text, string label)
    {
        Assert.True(AgentHostName.TryParse(text, out var name));
        Assert.Equal(label, name!.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-studio")]
    [InlineData("studio-")]
    [InlineData("my studio")]
    [InlineData("studio.lan")]
    [InlineData("a.b.local")]
    [InlineData("salón")]
    public void RefusesWhatIsNotOneDnsLabel(string text)
    {
        Assert.False(AgentHostName.TryParse(text, out var name));
        Assert.Null(name);
    }

    [Fact]
    public void RefusesALabelLongerThan63Characters()
    {
        Assert.True(AgentHostName.TryParse(new string('a', 63), out _));
        Assert.False(AgentHostName.TryParse(new string('a', 64), out _));
    }
}
