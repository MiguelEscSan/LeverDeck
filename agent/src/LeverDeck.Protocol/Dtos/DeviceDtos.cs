namespace LeverDeck.Protocol.Dtos;

public sealed record DeviceDto(
    Guid Id,
    string Name,
    DateTimeOffset PairedAt,
    DateTimeOffset LastSeenAt,
    bool Connected,
    bool Revoked);

/// <summary>The active pairing session. <see cref="FallbackUrl"/> uses the PC's IP and is null when it is unknown.</summary>
public sealed record PairingSessionDto(string Code, string QrUrl, string? FallbackUrl, DateTimeOffset ExpiresAt);

/// <summary>Body of <c>POST /api/pairing/redeem</c>.</summary>
public sealed record PairDeviceRequest(string Code, string DeviceName);

/// <summary>Success body of <c>POST /api/pairing/redeem</c>; the token is delivered only this once.</summary>
public sealed record PairDeviceResponse(Guid DeviceId, string DeviceToken)
{
    /// <summary>Redacted so the token never ends up in logs.</summary>
    public override string ToString() => $"PairDeviceResponse {{ DeviceId = {DeviceId}, DeviceToken = *** }}";
}
