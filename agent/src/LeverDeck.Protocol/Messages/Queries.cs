namespace LeverDeck.Protocol.Messages;

public sealed record GetProfilesQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record GetProfileQuery(string RequestId, ProfileIdPayload Payload) : QueryMessage(RequestId);

public sealed record GetDevicesQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record GetPairingSessionQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record GetSettingsQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record GetAgentStatusQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record GetInstalledGamesQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record ProfilesResult(string RequestId, ProfilesPayload Payload) : QueryResultMessage(RequestId);

public sealed record ProfileResult(string RequestId, ProfilePayload Payload) : QueryResultMessage(RequestId);

public sealed record DevicesResult(string RequestId, DevicesPayload Payload) : QueryResultMessage(RequestId);

public sealed record PairingSessionResult(string RequestId, PairingSessionPayload Payload) : QueryResultMessage(RequestId);

public sealed record SettingsResult(string RequestId, SettingsPayload Payload) : QueryResultMessage(RequestId);

public sealed record AgentStatusResult(string RequestId, AgentStatusPayload Payload) : QueryResultMessage(RequestId);

public sealed record InstalledGamesResult(string RequestId, InstalledGamesPayload Payload) : QueryResultMessage(RequestId);

public sealed record GetIconsQuery(string RequestId) : QueryMessage(RequestId)
{
    public EmptyPayload Payload { get; init; } = EmptyPayload.Instance;
}

public sealed record IconsResult(string RequestId, IconsPayload Payload) : QueryResultMessage(RequestId);
