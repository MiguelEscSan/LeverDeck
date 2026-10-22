using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Devices.Ports;

/// <summary>Turns a raw device token into the hash that is stored and looked up. It must be deterministic.</summary>
public interface ITokenHasher
{
    TokenHash Hash(string rawToken);
}
