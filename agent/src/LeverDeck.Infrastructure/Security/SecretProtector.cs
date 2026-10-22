using System.Security.Cryptography;
using System.Text;

namespace LeverDeck.Infrastructure.Security;

/// <summary>Encrypts small secrets before they are written under the user profile.</summary>
internal interface ISecretProtector
{
    string Protect(string secret);

    /// <exception cref="CryptographicException">The value was not produced by <see cref="Protect"/> for this user.</exception>
    string Unprotect(string protectedSecret);
}

/// <summary>
/// DPAPI with the current user's key: only this Windows account on this PC can decrypt. The purpose string is mixed
/// in as entropy, so a value protected for one purpose cannot be replayed as another.
/// </summary>
internal sealed class DpapiSecretProtector(string purpose) : ISecretProtector
{
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes($"LeverDeck/{purpose}");

    public string Protect(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string Unprotect(string protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);
        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(protectedSecret);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The protected value is not valid base64.", exception);
        }

        return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser));
    }
}
