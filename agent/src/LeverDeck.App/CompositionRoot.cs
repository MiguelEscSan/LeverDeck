using LeverDeck.Application.Actions;
using LeverDeck.Application.Devices;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Profiles;
using LeverDeck.Application.Settings;
using LeverDeck.Application.Status;
using LeverDeck.Infrastructure.DependencyInjection;

namespace LeverDeck.App;

/// <summary>Wires the adapters into the use cases. No logic lives here.</summary>
internal static class CompositionRoot
{
    public static IServiceCollection AddLeverDeck(this IServiceCollection services)
    {
        services.AddLeverDeckInfrastructure();
        services.AddLeverDeckUseCases();
        services.AddHostedService<AdminEditorAnnouncer>();
#if DEBUG
        services.AddDebugEndpointServices();
#endif
        return services;
    }

    /// <summary>The 18 commands and queries; they are stateless, so one instance each.</summary>
    public static IServiceCollection AddLeverDeckUseCases(this IServiceCollection services)
    {
        services.AddSingleton<ExecuteAction>();

        services.AddSingleton<ConnectDevice>();
        services.AddSingleton<GetDevices>();
        services.AddSingleton<RenameDevice>();
        services.AddSingleton<RevokeDevice>();

        services.AddSingleton<StartPairing>();
        services.AddSingleton<CancelPairing>();
        services.AddSingleton<GetPairingSession>();
        services.AddSingleton<PairDevice>();

        services.AddSingleton<GetProfiles>();
        services.AddSingleton<GetProfile>();
        services.AddSingleton<SaveProfile>();
        services.AddSingleton<DeleteProfile>();

        services.AddSingleton<GetSettings>();
        services.AddSingleton<UpdateSettings>();
        services.AddSingleton<RegisterApp>();
        services.AddSingleton<UnregisterApp>();

        services.AddSingleton<GetAgentStatus>();
        return services;
    }

    public static WebApplication MapLeverDeckEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => TypedResults.Text("ok"));
#if DEBUG
        if (app.Environment.IsDevelopment())
        {
            app.MapDebugEndpoints();
        }
#endif
        return app;
    }
}
