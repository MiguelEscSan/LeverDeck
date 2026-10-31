using LeverDeck.Application.Common;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Status;

/// <summary>The agent's versions and whether it is connected to OBS.</summary>
public sealed record AgentStatus(int ProtocolVersion, string AgentVersion, bool ObsConnected);

/// <summary>Query (device scope): the agent's protocol and informational versions, and the OBS connection state.</summary>
public sealed class GetAgentStatus(IObsClient obs)
{
    public Task<Result<AgentStatus>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, () =>
            Task.FromResult(Result.Success(new AgentStatus(
                ApplicationInfo.ProtocolVersion,
                ApplicationInfo.AgentVersion,
                obs.IsConnected))));
}
