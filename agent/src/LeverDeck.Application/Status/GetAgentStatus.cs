using System.Diagnostics.CodeAnalysis;
using LeverDeck.Application.Common;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Status;

/// <summary>The agent's versions. The OBS connection state is added in Phase 7.</summary>
public sealed record AgentStatus(int ProtocolVersion, string AgentVersion);

/// <summary>Query (device scope): the agent's protocol and informational versions.</summary>
public sealed class GetAgentStatus
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Every use case is an instance with one ExecuteAsync.")]
    public Task<Result<AgentStatus>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, () =>
            Task.FromResult(Result.Success(new AgentStatus(ApplicationInfo.ProtocolVersion, ApplicationInfo.AgentVersion))));
}
