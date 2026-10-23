using System.Net;
using LeverDeck.App;
using LeverDeck.App.Desktop;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Tls;
using Microsoft.Extensions.DependencyInjection;
using Velopack;

// First of all (Velopack's rule): the installer's install, update and uninstall hooks run here and exit, and an update
// downloaded by UpdateCheck is applied (the agent restarts with the same arguments).
VelopackApp.Build()
    .OnBeforeUninstallFastCallback(_ => Installer.BeforeUninstall())
    .Run();

var launch = Launch.FromArguments(args);
var builder = WebApplication.CreateSlimBuilder(Launch.HostArguments(args));

var options = LeverDeckOptions.FromConfiguration(builder.Configuration);

// One agent per Windows session. Decided before the log is started, so a second launch never deletes the running
// instance's log. Tests turn the desktop off and run many hosts in one process.
using var instance = options.DesktopEnabled && OperatingSystem.IsWindows() ? SingleInstance.Acquire() : null;
var action = Launch.Decide(instance?.IsFirst ?? true, launch);
if (!action.RunsTheAgent())
{
    if (action == InstanceAction.ShowRunningInstanceAndExit && OperatingSystem.IsWindows())
    {
        SingleInstance.ShowRunningInstance(TimeSpan.FromSeconds(10));
    }

    return;
}

#if !DEBUG
builder.Logging.AddLeverDeckFileLogging(options);
#endif
builder.Services.AddLeverDeck();
if (options.DesktopEnabled)
{
    builder.Services.AddLeverDeckDesktop(new DesktopLaunch(ShowWindowOnStart: action == InstanceAction.RunAndShowWindow));
}
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // The certificate is read when Kestrel starts; without HTTPS, or without a certificate, the agent serves plain HTTP.
    var certificate = options.HttpsEnabled
        ? kestrel.ApplicationServices!.GetRequiredService<LanCertificate>().LoadOrIssue()
        : null;
    kestrel.Listen(IPAddress.Any, LeverDeckOptions.Port, listen =>
    {
        if (certificate is not null)
        {
            listen.UseHttps(certificate);
        }
    });
});

var app = builder.Build();

app.MapLeverDeckEndpoints();

app.Run();

public partial class Program;
