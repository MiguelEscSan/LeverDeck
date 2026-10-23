using LeverDeck.Infrastructure.Platform;

namespace LeverDeck.Infrastructure.Tests.Platform;

public sealed class FirewallRuleTests
{
    [Fact]
    public void AddAllowsInboundTcpOnTheLanProfileOnly()
    {
        var arguments = PrivateFirewallRule.AddArguments(8765);

        Assert.Equal(
            ["advfirewall", "firewall", "add", "rule", "name=LeverDeck agent TCP 8765", "dir=in", "action=allow", "protocol=TCP",
                "localport=8765", "profile=private"],
            arguments);
    }

    [Fact]
    public void ManualCommandQuotesTheRuleName()
    {
        Assert.Equal(
            "netsh advfirewall firewall add rule \"name=LeverDeck agent TCP 8765\" dir=in action=allow protocol=TCP localport=8765 profile=private",
            PrivateFirewallRule.ManualCommand(8765));
    }

    [Fact]
    public void ShowLooksUpTheRuleByName()
    {
        Assert.Equal(["advfirewall", "firewall", "show", "rule", "name=LeverDeck agent TCP 8765"], PrivateFirewallRule.ShowArguments());
    }
}
