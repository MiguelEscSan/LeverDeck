using LeverDeck.Infrastructure.Icons;
using LeverDeck.Infrastructure.Sounds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// The agent's network surface (docs/ROADMAP.md, Phase 3.1). Handlers are plain <see cref="RequestDelegate"/>s with
/// source-generated JSON, so nothing here needs reflection under Native AOT.
/// </summary>
public static class TransportEndpoints
{
    public const string DevicePath = "/ws";
    public const string AdminPath = "/admin/ws";

    /// <summary>Pings idle sockets so dead phones (screen off, Wi-Fi lost) are noticed and dropped.</summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>A socket that does not answer a ping within this time is closed.</summary>
    public static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(15);

    public static TBuilder MapLeverDeckTransport<TBuilder>(this TBuilder app)
        where TBuilder : IApplicationBuilder, IEndpointRouteBuilder
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = KeepAliveInterval, KeepAliveTimeout = KeepAliveTimeout });

        app.MapPost(PairingRedeemEndpoint.Path, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<PairingRedeemEndpoint>().HandleAsync(context)));
        app.MapGet(IconEndpoint.Route, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<IconEndpoint>().HandleAsync(context)));
        app.MapPut(SoundEndpoint.Route, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<SoundEndpoint>().PutAsync(context)));
        app.MapGet(SoundEndpoint.Route, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<SoundEndpoint>().GetAsync(context)));
        app.Map(DevicePath, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<WebSocketSessions>().HandleDeviceAsync(context)));
        app.Map(AdminPath, (RequestDelegate)(context =>
            context.RequestServices.GetRequiredService<WebSocketSessions>().HandleAdminAsync(context)));
        return app;
    }

    /// <summary>The transport's services; the use cases they call are registered by the composition root.</summary>
    public static IServiceCollection AddLeverDeckTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ConnectionRegistry>();
        services.AddSingleton<PairingLinks>();
        services.AddSingleton<TransportPayloads>();
        services.AddSingleton<MessageRouter>();
        services.AddSingleton<WebSocketSessions>();
        services.AddSingleton<PairingRedeemEndpoint>();
        services.AddSingleton<IconEndpoint>();
        services.AddSingleton<SoundEndpoint>();
        services.AddSingleton<EventFanOut>();
        services.AddHostedService(provider => provider.GetRequiredService<EventFanOut>());
        return services;
    }
}
