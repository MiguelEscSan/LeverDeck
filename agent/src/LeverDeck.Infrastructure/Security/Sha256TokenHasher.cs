using System.Security.Cryptography;
using System.Text;
using LeverDeck.Application.Devices.Ports;
using LeverDeck.Domain.Devices;

namespace LeverDeck.Infrastructure.Security;

/// <summary>
/// SHA-256 of the UTF-8 token as lowercase hex. Device tokens are 256-bit random values, so a fast hash is enough:
/// there is nothing to brute-force that a slow KDF would protect.
/// </summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public TokenHash Hash(string rawToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);
        return new TokenHash(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))));
    }
}
