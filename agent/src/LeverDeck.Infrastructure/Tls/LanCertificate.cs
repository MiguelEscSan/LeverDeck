using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Tls;

/// <summary>
/// The optional HTTPS certificate for the LAN (docs/ARCHITECTURE.md, Connectivity). Issued with <c>mkcert</c> for
/// this PC's <see cref="AgentHostName"/>, loopback and the PC's LAN address, and stored under <c>tls/</c> in the data
/// directory. The owner runs <c>mkcert -install</c> on the PC and installs the root CA (<c>mkcert -CAROOT</c>) on each
/// phone once. Reissued when the set of names changes, for example after the LAN address or the host name changes.
/// </summary>
public sealed partial class LanCertificate(LeverDeckOptions options, AgentHostName hostName, ILogger<LanCertificate> logger)
{
    /// <summary>mkcert writes its PKCS#12 bundles with this fixed password.</summary>
    public const string Password = "changeit";

    private const string TlsFolder = "tls";
    private const string PfxFile = "leverdeck.p12";
    private const string NamesFile = "leverdeck.names";

    /// <summary>The names the certificate must cover: the mDNS name, loopback and the LAN address when there is one.</summary>
    public static IReadOnlyList<string> NamesFor(AgentHostName hostName, IPAddress? lanAddress)
    {
        ArgumentNullException.ThrowIfNull(hostName);
        var names = new List<string> { hostName.Fqdn, "localhost", "127.0.0.1" };
        if (lanAddress is not null)
        {
            names.Add(lanAddress.ToString());
        }

        return names;
    }

    /// <summary>A certificate is reissued when it is missing or was issued for other names.</summary>
    public static bool NeedsIssue(bool hasCertificate, string? storedNames, IReadOnlyList<string> desired) =>
        !hasCertificate || storedNames != string.Join(' ', desired);

    /// <summary>The certificate for HTTPS, issued or reissued as needed, or <see langword="null"/> when mkcert is not available.</summary>
    public X509Certificate2? LoadOrIssue()
    {
        var names = NamesFor(hostName, LanAddressSelector.Select(LanAddressSelector.FromSystem()));
        var directory = Path.Combine(options.DataDirectory, TlsFolder);
        var pfxPath = Path.Combine(directory, PfxFile);
        var namesPath = Path.Combine(directory, NamesFile);

        var storedNames = File.Exists(namesPath) ? File.ReadAllText(namesPath).Trim() : null;
        if (NeedsIssue(File.Exists(pfxPath), storedNames, names) && !Issue(directory, pfxPath, names))
        {
            return null;
        }

        File.WriteAllText(namesPath, string.Join(' ', names));
        return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, Password);
    }

    private bool Issue(string directory, string pfxPath, IReadOnlyList<string> names)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo("mkcert")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-pkcs12");
        start.ArgumentList.Add("-p12-file");
        start.ArgumentList.Add(pfxPath);
        foreach (var name in names)
        {
            start.ArgumentList.Add(name);
        }

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("mkcert could not be started.");
            var output = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                LogIssueFailed(process.ExitCode, output.Trim());
                return false;
            }

            LogIssued(names.Count);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            LogMkcertMissing();
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Issued the HTTPS certificate for {NameCount} names.")]
    private partial void LogIssued(int nameCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "mkcert failed with exit code {ExitCode}: {Output}")]
    private partial void LogIssueFailed(int exitCode, string output);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "HTTPS is enabled but mkcert was not found. Install mkcert, run 'mkcert -install', then restart the agent. Serving HTTP.")]
    private partial void LogMkcertMissing();
}
