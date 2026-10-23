using System.Diagnostics;
using System.Runtime.Versioning;

namespace LeverDeck.Infrastructure.Platform;

/// <summary>
/// Opens the agent port for inbound TCP on the private (home or work) network profile, so phones on the LAN can reach
/// it (docs/ARCHITECTURE.md, Connectivity). Uses <c>netsh</c>, which needs administrator rights to add the rule: when
/// the agent runs without them the rule is not added and the caller reports the command to run by hand.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PrivateFirewallRule
{
    public const string RuleName = "LeverDeck agent TCP 8765";

    /// <summary>Makes sure the rule exists and reports what happened.</summary>
    public static FirewallRuleResult EnsureInstalled(int port)
    {
        if (Run(ShowArguments()) == 0)
        {
            return FirewallRuleResult.AlreadyPresent;
        }

        return Run(AddArguments(port)) == 0 ? FirewallRuleResult.Added : FirewallRuleResult.NotAllowed;
    }

    /// <summary>The <c>netsh</c> call that adds the rule, for the owner to run from an elevated prompt.</summary>
    public static string ManualCommand(int port) =>
        "netsh " + string.Join(' ', AddArguments(port).Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));

    public static IReadOnlyList<string> ShowArguments() => ["advfirewall", "firewall", "show", "rule", $"name={RuleName}"];

    public static IReadOnlyList<string> AddArguments(int port) =>
    [
        "advfirewall", "firewall", "add", "rule", $"name={RuleName}", "dir=in", "action=allow", "protocol=TCP",
        $"localport={port}", "profile=private",
    ];

    private static int Run(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("netsh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("netsh could not be started.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }
}

public enum FirewallRuleResult
{
    AlreadyPresent,
    Added,
    NotAllowed,
}
