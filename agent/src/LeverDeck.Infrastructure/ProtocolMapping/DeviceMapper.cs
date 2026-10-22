using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Pairing;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Maps devices for the editor. Whether one is connected is known only to the transport.</summary>
public static class DeviceMapper
{
    public static DeviceDto ToDto(Device device, bool connected)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new DeviceDto(device.Id.Value, device.Name.Value, device.PairedAt, device.LastSeenAt, connected, device.IsRevoked);
    }
}

/// <summary>Maps the active pairing session. The transport builds the URLs, since only it knows host and port.</summary>
public static class PairingSessionMapper
{
    public static PairingSessionDto ToDto(PairingSession session, string qrUrl, string? fallbackUrl)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrUrl);
        return new PairingSessionDto(session.Code.Value, qrUrl, fallbackUrl, session.ExpiresAt);
    }
}
