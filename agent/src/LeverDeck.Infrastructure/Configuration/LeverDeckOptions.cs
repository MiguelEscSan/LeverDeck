using System.Globalization;
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

    public const string MdnsHostNameKey = "LeverDeck:Mdns:HostName";

    public const string ConnectTimeoutKey = "LeverDeck:Transport:ConnectTimeout";

    public const string HttpsEnabledKey = "LeverDeck:Https:Enabled";

    public const string DesktopEnabledKey = "LeverDeck:Desktop:Enabled";

    /// <summary>HTTP and WebSocket port (docs/ARCHITECTURE.md, Connectivity).</summary>
    public const int Port = 8765;

    /// <summary>Absolute folder holding <c>profiles.json</c>, <c>devices.json</c>, <c>settings.json</c> and the admin token.</summary>
    public required string DataDirectory { get; init; }

    /// <summary>The run's log file in Release builds (docs/ARCHITECTURE.md, Logs).</summary>
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>Whether the agent advertises its host name over mDNS. Tests turn it off.</summary>
    public bool MdnsEnabled { get; init; } = true;

    /// <summary>
    /// The name this PC answers to instead of the one generated from the machine name (a label or <c>label.local</c>),
    /// or <see langword="null"/>. For two PCs whose names would otherwise collide.
    /// </summary>
    public string? MdnsHostName { get; init; }

    /// <summary>How long a new <c>/ws</c> connection may take to send <c>connectDevice</c> before it is closed.</summary>
    public TimeSpan ConnectTimeout { get; init; } = DefaultConnectTimeout;

    public static TimeSpan DefaultConnectTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Serve HTTPS on the same port with a <c>mkcert</c> certificate (docs/ARCHITECTURE.md, Connectivity). Off by
    /// default: the device must then trust the mkcert root CA.
    /// </summary>
    public bool HttpsEnabled { get; init; }

    /// <summary>
    /// The tray icon and the firewall rule, which only make sense on the owner's desktop. Tests turn them off.
    /// </summary>
    public bool DesktopEnabled { get; init; } = true;

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
            MdnsHostName = string.IsNullOrWhiteSpace(configuration[MdnsHostNameKey]) ? null : configuration[MdnsHostNameKey]!.Trim(),
            ConnectTimeout = ReadDuration(configuration, ConnectTimeoutKey, DefaultConnectTimeout),
            HttpsEnabled = ReadFlag(configuration, HttpsEnabledKey, defaultValue: false),
            DesktopEnabled = ReadFlag(configuration, DesktopEnabledKey, defaultValue: true),
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

    private static TimeSpan ReadDuration(IConfiguration configuration, string key, TimeSpan defaultValue)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var value) && value > TimeSpan.Zero
            ? value
            : throw new InvalidOperationException($"{key} must be a positive duration such as '00:00:10', got '{raw}'.");
    }
}
