using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Pairing;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class DeviceAndPairingMapperTests
{
    private static readonly DateTimeOffset _pairedAt = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Device_MapsEveryFieldAndTheConnectionState()
    {
        var id = new DeviceId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var device = Device.Restore(id, new DeviceName("Pixel"), new TokenHash("hash"), _pairedAt, _pairedAt.AddHours(1), null);

        var dto = DeviceMapper.ToDto(device, connected: true);

        Assert.Equal(new DeviceDto(id.Value, "Pixel", _pairedAt, _pairedAt.AddHours(1), true, false), dto);
    }

    [Fact]
    public void RevokedDevice_IsMarkedRevoked()
    {
        var device = Device.Pair(new DeviceName("Tablet"), new TokenHash("hash"), _pairedAt);
        device.Revoke(_pairedAt.AddMinutes(1));

        var dto = DeviceMapper.ToDto(device, connected: false);

        Assert.True(dto.Revoked);
        Assert.False(dto.Connected);
    }

    [Fact]
    public void PairingSession_CarriesCodeUrlsAndExpiry()
    {
        var session = PairingSession.Start(new PairingCode("123456"), _pairedAt);

        var dto = PairingSessionMapper.ToDto(session, "http://leverdeck.local:8765/pair?code=123456", null);

        Assert.Equal(
            new PairingSessionDto("123456", "http://leverdeck.local:8765/pair?code=123456", null, _pairedAt + PairingSession.Lifetime),
            dto);
    }
}
