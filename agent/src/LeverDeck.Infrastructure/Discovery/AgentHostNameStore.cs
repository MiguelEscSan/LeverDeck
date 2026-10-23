using System.Security.Cryptography;
using System.Text;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// Decides this PC's <see cref="AgentHostName"/>: <see cref="LeverDeckOptions.MdnsHostName"/> when configured, else the
/// name stored in <c>hostname</c> in the data directory, else one generated from the machine name and stored there.
/// Stored once so that renaming the PC does not move it to another origin, which would unpair every device.
/// </summary>
public sealed class AgentHostNameStore
{
    private const string FileName = "hostname";
    private const string SuffixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly LeverDeckOptions _options;
    private readonly Func<string> _machineName;
    private readonly Func<string> _randomSuffix;

    public AgentHostNameStore(LeverDeckOptions options)
        : this(options, () => Environment.MachineName, () => RandomNumberGenerator.GetString(SuffixAlphabet, 6))
    {
    }

    internal AgentHostNameStore(LeverDeckOptions options, Func<string> machineName, Func<string> randomSuffix)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _machineName = machineName;
        _randomSuffix = randomSuffix;
    }

    public AgentHostName Resolve()
    {
        if (_options.MdnsHostName is { } configured)
        {
            return AgentHostName.TryParse(configured, out var chosen)
                ? chosen!
                : throw new InvalidOperationException(
                    $"{LeverDeckOptions.MdnsHostNameKey} must be one DNS label (1–63 letters, digits or dashes, not "
                    + $"starting or ending with a dash), optionally followed by '.local'; got '{configured}'.");
        }

        var path = Path.Combine(_options.DataDirectory, FileName);
        if (File.Exists(path) && AgentHostName.TryParse(File.ReadAllText(path), out var stored))
        {
            return stored!;
        }

        // Atomic: a write cut short must not leave a shorter label that still parses and moves the PC to another origin.
        var name = AgentHostName.FromMachineName(_machineName(), _randomSuffix);
        AtomicFile.WriteAllBytes(path, Encoding.UTF8.GetBytes(name.Label));
        return name;
    }
}
