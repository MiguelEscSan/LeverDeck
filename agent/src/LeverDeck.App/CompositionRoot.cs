using LeverDeck.App.Desktop;
using LeverDeck.App.Platform;
using LeverDeck.App.Tray;
using LeverDeck.Application.Actions;
using LeverDeck.Application.Devices;
using LeverDeck.Application.Games;
using LeverDeck.Application.Icons;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Profiles;
using LeverDeck.Application.Settings;
using LeverDeck.Application.Status;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.DependencyInjection;
using LeverDeck.Infrastructure.Desktop;
using LeverDeck.Infrastructure.Logging;
using LeverDeck.Infrastructure.Transport;

namespace LeverDeck.App;

/// <summary>Wires the adapters into the use cases. No logic lives here.</summary>
internal static class CompositionRoot
{
    public static IServiceCollection AddLeverDeck(this IServiceCollection services)
    {
        services.AddLeverDeckInfrastructure();
        services.AddLeverDeckUseCases();
        services.AddHostedService<DefaultProfileSeeder>();
        services.AddHostedService<AdminEditorAnnouncer>();
#if DEBUG
        services.AddDebugEndpointServices();
#endif
        return services;
    }

    /// <summary>
    /// Release logging: no console (the exe is WinExe), so entries go to <c>&lt;DataDirectory&gt;/logs</c> only. The
    /// file is started, which deletes the logs of earlier runs, when the host builds its logger factory; registered
    /// through a factory so the container disposes it on exit.
    /// </summary>
    public static ILoggingBuilder AddLeverDeckFileLogging(this ILoggingBuilder logging, LeverDeckOptions options)
    {
        logging.ClearProviders();
        // The release ships the exe alone, without appsettings.json: keep ASP.NET Core's per-request lines out of the log.
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        logging.Services.AddSingleton<ILoggerProvider>(provider => new FileLoggerProvider(
            SessionLogFile.Start(options.LogDirectory),
            provider.GetService<TimeProvider>() ?? TimeProvider.System));
        return logging;
    }

    /// <summary>
    /// The tray icon, the editor window and the firewall rule, for the owner's desktop only
    /// (<c>LeverDeck:Desktop:Enabled</c>).
    /// </summary>
    public static IServiceCollection AddLeverDeckDesktop(this IServiceCollection services, DesktopLaunch launch)
    {
        services.AddSingleton(launch);
        services.AddSingleton<WindowPlacementStore>();
        services.AddSingleton<EditorWindow>();
        services.AddSingleton<TrayIcon>();
        services.AddHostedService(provider => provider.GetRequiredService<TrayIcon>());
        services.AddHostedService<FirewallRuleService>();
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
        services.AddSingleton<GetInstalledGames>();
        services.AddSingleton<GetStoreApps>();

        services.AddSingleton<SaveIcon>();
        services.AddSingleton<DeleteIcon>();
        services.AddSingleton<GetIcons>();
        return services;
    }

    public static WebApplication MapLeverDeckEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => TypedResults.Text("ok"));
        app.MapLeverDeckTransport();
#if DEBUG
        if (app.Environment.IsDevelopment())
        {
            app.MapDebugEndpoints();
        }
#endif
        app.UseLeverDeckWebUi();
        return app;
    }
}
