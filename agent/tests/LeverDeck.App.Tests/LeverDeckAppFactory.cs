using LeverDeck.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LeverDeck.App.Tests;

/// <summary>
/// The real composition root, isolated from the owner's PC: files go to a fresh temp folder (never %APPDATA%), the
/// mDNS advertiser is off and the host name is fixed, so links do not depend on the machine running the tests. Tests
/// never call an endpoint that could inject input.
/// </summary>
public class LeverDeckAppFactory : WebApplicationFactory<Program>
{
    public const string HostName = "leverdeck-test";

    public LeverDeckAppFactory()
        : this("Production")
    {
    }

    protected LeverDeckAppFactory(string environment)
    {
        Environment = environment;
        DataDirectory = Path.Combine(Path.GetTempPath(), "leverdeck-tests", Guid.NewGuid().ToString("N"));
    }

    public string Environment { get; }

    public string DataDirectory { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting(LeverDeckOptions.DataDirectoryKey, DataDirectory);
        builder.UseSetting(LeverDeckOptions.MdnsEnabledKey, "false");
        builder.UseSetting(LeverDeckOptions.MdnsHostNameKey, HostName);
        builder.UseSetting(LeverDeckOptions.DesktopEnabledKey, "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}

public sealed class DevelopmentAppFactory() : LeverDeckAppFactory("Development");
