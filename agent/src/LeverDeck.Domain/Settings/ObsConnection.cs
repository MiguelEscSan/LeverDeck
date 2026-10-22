using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Settings;

/// <summary>
/// How the agent reaches OBS WebSocket: trimmed host (1–253 characters), port 1–65535 and an optional password of
/// at most 256 characters (an empty one means none). Protecting the password at rest is Infrastructure's job.
/// </summary>
public sealed record ObsConnection
{
    public const int MaxHostLength = 253;
    public const int MaxPasswordLength = 256;
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public ObsConnection(string host, int port, string? password)
    {
        var trimmedHost = host?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmedHost.Length is >= 1 and <= MaxHostLength,
            DomainErrorCode.ValidationFailed,
            $"OBS host must be 1–{MaxHostLength} characters.");
        Invariant.Require(
            port is >= MinPort and <= MaxPort,
            DomainErrorCode.ValidationFailed,
            $"OBS port must be {MinPort}–{MaxPort}.");
        Invariant.Require(
            password is null || password.Length <= MaxPasswordLength,
            DomainErrorCode.ValidationFailed,
            $"OBS password must be at most {MaxPasswordLength} characters.");

        Host = trimmedHost;
        Port = port;
        Password = string.IsNullOrEmpty(password) ? null : password;
    }

    public string Host { get; }

    public int Port { get; }

    public string? Password { get; }

    public bool HasPassword => Password is not null;

    /// <summary>Redacted so the password never ends up in logs.</summary>
    public override string ToString() => $"{Host}:{Port} (password {(HasPassword ? "set" : "not set")})";
}
