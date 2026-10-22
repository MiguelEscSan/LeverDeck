using System.Security.Cryptography;
using System.Text;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Security;

/// <summary>
/// The persistent admin token of the desktop editor (docs/ARCHITECTURE.md, Desktop editor). Generated once, stored
/// DPAPI-protected in <c>admin-token</c> under the data directory and reused across restarts. Not an Application
/// port: only the transport needs it, to guard <c>/admin/ws</c>.
/// </summary>
public sealed class AdminTokenStore
{
    public const string FileName = "admin-token";

    private readonly ISecretProtector _protector;
    private readonly Lock _gate = new();
    private string? _token;

    public AdminTokenStore(LeverDeckOptions options)
        : this(options, new DpapiSecretProtector("admin-token"))
    {
    }

    internal AdminTokenStore(LeverDeckOptions options, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(options);
        FilePath = Path.Combine(options.DataDirectory, FileName);
        _protector = protector;
    }

    public string FilePath { get; }

    /// <summary>
    /// The stored token, creating it on first use. A file that cannot be decrypted (corrupt, or protected by another
    /// Windows user) fails loudly and is left as it is, so a bookmarked editor URL is never silently invalidated.
    /// </summary>
    public string GetOrCreate()
    {
        lock (_gate)
        {
            return _token ??= File.Exists(FilePath) ? Load() : Create();
        }
    }

    /// <summary>Compares a presented token with the stored one in constant time.</summary>
    public bool Verify(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        // Hashing first gives both sides the same length, so the comparison time never depends on the input.
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(GetOrCreate())),
            SHA256.HashData(Encoding.UTF8.GetBytes(candidate)));
    }

    private string Load()
    {
        var stored = File.ReadAllText(FilePath).Trim();
        try
        {
            return _protector.Unprotect(stored);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                $"{FilePath} cannot be decrypted for this Windows user; it was left untouched. Delete it to issue a new admin token.",
                exception);
        }
    }

    private string Create()
    {
        var token = SecureTokenGenerator.NewToken();
        AtomicFile.WriteAllBytes(FilePath, Encoding.UTF8.GetBytes(_protector.Protect(token)));
        return token;
    }
}
