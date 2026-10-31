using LeverDeck.Application.Actions.Ports;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Devices.Ports;
using LeverDeck.Application.Games.Ports;
using LeverDeck.Application.Icons.Ports;
using LeverDeck.Application.Obs.Ports;
using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Application.Platform.Ports;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Infrastructure.Common;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;
using LeverDeck.Infrastructure.Games;
using LeverDeck.Infrastructure.Icons;
using LeverDeck.Infrastructure.Apps;
using LeverDeck.Infrastructure.Input;
using LeverDeck.Infrastructure.Obs;
using LeverDeck.Infrastructure.Pairing;
using LeverDeck.Infrastructure.Persistence;
using LeverDeck.Infrastructure.Platform;
using LeverDeck.Infrastructure.Security;
using LeverDeck.Infrastructure.Tls;
using LeverDeck.Infrastructure.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers every Windows adapter behind its Application port, all as singletons (they are thread-safe and the
    /// pairing store must be shared). Options are read from <see cref="IConfiguration"/> when first needed.
    /// </summary>
    public static IServiceCollection AddLeverDeckInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider => LeverDeckOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(provider => new AgentHostNameStore(provider.GetRequiredService<LeverDeckOptions>()).Resolve());
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<InMemoryEventBroadcaster>();
        services.AddSingleton<IEventPublisher>(provider => provider.GetRequiredService<InMemoryEventBroadcaster>());

        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<IDeviceRepository, JsonDeviceRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<FileIconStore>();
        services.AddSingleton<IIconStore>(provider => provider.GetRequiredService<FileIconStore>());
        services.AddSingleton<IPairingSessionStore, InMemoryPairingSessionStore>();

        services.AddSingleton<ITokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<AdminTokenStore>();
        services.AddSingleton<LanCertificate>();
        services.AddSingleton<IStartupRegistration, RunKeyStartupRegistration>();

        services.AddSingleton<IActionExecutor, AgentActionExecutor>();
        services.AddSingleton<IGameLibrary, InstalledGameLibrary>();
        services.AddSingleton<IStoreAppCatalog, StartMenuStoreAppCatalog>();

        services.AddSingleton<ObsWebSocketClient>();
        services.AddSingleton<IObsClient>(provider => provider.GetRequiredService<ObsWebSocketClient>());
        services.AddHostedService<ObsConnectionWorker>();

        // mDNS: our own responder answers A queries for this PC's host name (Windows does not for DnsServiceRegister's
        // target host); the native registration still publishes the _http._tcp service records.
        services.AddSingleton(provider => new MdnsHostNameResponder(
            provider.GetRequiredService<ILogger<MdnsHostNameResponder>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<AgentHostName>()));
        services.AddSingleton<DnsServiceAdvertiser>();
        services.AddSingleton<IServiceAdvertiser>(provider => new CompositeServiceAdvertiser(
        [
            provider.GetRequiredService<MdnsHostNameResponder>(),
            provider.GetRequiredService<DnsServiceAdvertiser>(),
        ]));
        services.AddHostedService<ServiceAdvertisementHostedService>();

        services.AddLeverDeckTransport();

        return services;
    }
}
