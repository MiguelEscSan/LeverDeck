using LeverDeck.Application.Actions.Ports;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Devices.Ports;
using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Infrastructure.Common;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;
using LeverDeck.Infrastructure.Input;
using LeverDeck.Infrastructure.Pairing;
using LeverDeck.Infrastructure.Persistence;
using LeverDeck.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<InMemoryEventBroadcaster>();
        services.AddSingleton<IEventPublisher>(provider => provider.GetRequiredService<InMemoryEventBroadcaster>());

        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<IDeviceRepository, JsonDeviceRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<IPairingSessionStore, InMemoryPairingSessionStore>();

        services.AddSingleton<ITokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<AdminTokenStore>();

        services.AddSingleton<IActionExecutor, SendInputActionExecutor>();

        services.AddSingleton<IServiceAdvertiser, DnsServiceAdvertiser>();
        services.AddHostedService<ServiceAdvertisementHostedService>();

        return services;
    }
}
