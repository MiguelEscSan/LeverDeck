using Microsoft.Extensions.Configuration;

namespace LeverDeck.Infrastructure.Configuration;

/// <summary>
/// Agent settings that come from the host configuration (appsettings, <c>LeverDeck__*</c> environment variables or
/// command-line switches), not from <c>settings.json</c>. Read by hand instead of with the configuration binder, which
/// relies on reflection that Native AOT trims.
/// </summary>
public sealed record LeverDeckOptions
{
    public const string DataDirectoryKey = "LeverDeck:DataDirectory";

    public const string MdnsEnabledKey = "LeverDeck:Mdns:Enabled";

    /// <summary>HTTP and WebSocket port (docs/ARCHITECTURE.md, Connectivity).</summary>
    public const int Port = 8765;

    /// <summary>Absolute folder holding <c>profiles.json</c>, <c>devices.json</c>, <c>settings.json</c> and the admin token.</summary>
    public required string DataDirectory { get; init; }

    /// <summary>Whether the agent advertises itself as <c>leverdeck.local</c>. Tests turn it off.</summary>
    public bool MdnsEnabled { get; init; } = true;

    /// <summary><c>%APPDATA%\LeverDeck</c>, resolved through the known folder rather than hardcoded.</summary>
    public static string DefaultDataDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            throw new InvalidOperationException("The roaming application data folder is not available for this user.");
        }

        return Path.Combine(appData, "LeverDeck");
    }

    public static LeverDeckOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var dataDirectory = configuration[DataDirectoryKey];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = DefaultDataDirectory();
        }
        else if (!Path.IsPathFullyQualified(dataDirectory))
        {
            throw new InvalidOperationException($"{DataDirectoryKey} must be an absolute path, got '{dataDirectory}'.");
        }

        return new LeverDeckOptions
        {
            DataDirectory = dataDirectory,
            MdnsEnabled = ReadFlag(configuration, MdnsEnabledKey, defaultValue: true),
        };
    }

    private static bool ReadFlag(IConfiguration configuration, string key, bool defaultValue)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return bool.TryParse(raw, out var value)
            ? value
            : throw new InvalidOperationException($"{key} must be 'true' or 'false', got '{raw}'.");
    }
}
