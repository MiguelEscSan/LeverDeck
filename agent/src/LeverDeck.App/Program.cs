using System.Net;
using LeverDeck.App;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Tls;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

var options = LeverDeckOptions.FromConfiguration(builder.Configuration);
#if !DEBUG
builder.Logging.AddLeverDeckFileLogging(options);
#endif
builder.Services.AddLeverDeck();
if (options.DesktopEnabled)
{
    builder.Services.AddLeverDeckDesktop();
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
