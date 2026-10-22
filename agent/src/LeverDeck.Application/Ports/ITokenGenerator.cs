using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Ports;

/// <summary>Cryptographically random secrets.</summary>
public interface ITokenGenerator
{
    /// <summary>A new raw device token. It is handed to the device once and never stored.</summary>
    string NewDeviceToken();

    PairingCode NewPairingCode();
}
