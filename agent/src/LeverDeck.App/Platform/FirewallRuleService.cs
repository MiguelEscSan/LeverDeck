using System.Runtime.Versioning;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Platform;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.App.Platform;

/// <summary>
/// Makes sure the private-network firewall rule for the agent port exists (Phase 7). Runs in the background so netsh does
/// not delay the start. Without administrator rights the rule cannot be added: the agent logs the command to run once,
/// elevated, and keeps serving on the loopback address.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class FirewallRuleService(ILogger<FirewallRuleService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(EnsureRule, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void EnsureRule()
    {
        try
        {
            switch (PrivateFirewallRule.EnsureInstalled(LeverDeckOptions.Port))
            {
                case FirewallRuleResult.Added:
                    LogAdded(LeverDeckOptions.Port);
                    break;
                case FirewallRuleResult.NotAllowed:
                    LogNotAllowed(PrivateFirewallRule.ManualCommand(LeverDeckOptions.Port));
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogFailed(exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened TCP port {Port} on the private network profile.")]
    private partial void LogAdded(int port);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The firewall rule for the private network could not be added without administrator rights. Phones may not reach the agent. Run once from an elevated prompt: {Command}")]
    private partial void LogNotAllowed(string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The firewall rule could not be checked: {Message}")]
    private partial void LogFailed(string message);
}
