using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Infrastructure.Security;

/// <summary>Secrets from the OS cryptographic random generator.</summary>
public sealed class SecureTokenGenerator : ITokenGenerator
{
    private const int TokenBytes = 32;

    /// <summary>32 random bytes as unpadded base64url (43 characters), safe in URLs and headers.</summary>
    public string NewDeviceToken() => NewToken();

    /// <summary>A uniformly random code from 000000 to 999999.</summary>
    public PairingCode NewPairingCode() =>
        new(RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture));

    internal static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
}
