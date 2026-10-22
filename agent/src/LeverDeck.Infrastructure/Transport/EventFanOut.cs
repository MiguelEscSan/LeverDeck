using System.Net.WebSockets;
using System.Text.Json;
using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Infrastructure.Common;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using LeverDeck.Protocol.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// Turns application events into <c>event</c> messages and pushes them to the live connections of the right scope:
/// profile changes to everyone, devices/pairing/settings to the editor only, <c>deviceRevoked</c> to the revoked
/// device, whose sockets are closed right after. Payloads are loaded as the admin, since only the agent reads them.
/// A connection that cannot be reached is skipped; its own session cleans it up.
/// </summary>
public sealed partial class EventFanOut(
    InMemoryEventBroadcaster broadcaster,
    ConnectionRegistry registry,
    TransportPayloads payloads,
    ILogger<EventFanOut> logger) : IHostedService, IDisposable
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = broadcaster.Subscribe(HandleAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>
    /// Pushes the device list to the editor. Also called by the sessions when a device connects or disconnects,
    /// since the <c>connected</c> flag is transport state that no use case knows about.
    /// </summary>
    public async Task PushDevicesAsync(CancellationToken cancellationToken)
    {
        var admins = registry.Admins();
        if (admins.Count == 0)
        {
            return;
        }

        if (Loaded(await payloads.DevicesAsync(Caller.Admin, cancellationToken).ConfigureAwait(false)) is { } devices)
        {
            await SendAsync(admins, new DevicesChangedEvent(devices)).ConfigureAwait(false);
        }
    }

    internal async Task HandleAsync(ApplicationEvent applicationEvent, CancellationToken cancellationToken)
    {
        switch (applicationEvent)
        {
            case ProfileUpdated updated:
                if (Loaded(await payloads.ProfileAsync(Caller.Admin, updated.ProfileId, cancellationToken).ConfigureAwait(false)) is { } profile)
                {
                    await SendAsync(registry.All(), new ProfileUpdatedEvent(profile)).ConfigureAwait(false);
                }

                break;

            case ProfileDeleted deleted:
                await SendAsync(registry.All(), new ProfileDeletedEvent(new ProfileIdPayload(deleted.ProfileId.Value))).ConfigureAwait(false);
                break;

            case DevicesChanged:
                await PushDevicesAsync(cancellationToken).ConfigureAwait(false);
                break;

            case DeviceRevoked revoked:
            {
                var sockets = registry.OfDevice(revoked.DeviceId);
                await SendAsync(sockets, new DeviceRevokedEvent(new DeviceIdPayload(revoked.DeviceId.Value))).ConfigureAwait(false);
                await Task.WhenAll(sockets.Select(socket =>
                    socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, CloseReasons.For(ErrorCodeDto.DeviceRevoked)))).ConfigureAwait(false);
                break;
            }

            case PairingSessionChanged:
                if (Loaded(await payloads.PairingSessionAsync(Caller.Admin, cancellationToken).ConfigureAwait(false)) is { } session)
                {
                    await SendAsync(registry.Admins(), new PairingSessionChangedEvent(session)).ConfigureAwait(false);
                }

                break;

            case SettingsUpdated:
                if (Loaded(await payloads.SettingsAsync(Caller.Admin, cancellationToken).ConfigureAwait(false)) is { } settings)
                {
                    await SendAsync(registry.Admins(), new SettingsUpdatedEvent(settings)).ConfigureAwait(false);
                }

                break;

            default:
                LogUnhandled(applicationEvent.GetType().Name);
                break;
        }
    }

    private T? Loaded<T>(Result<T> result)
        where T : class
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        // For example a profile deleted right after it was saved: there is nothing left to push.
        LogPayloadUnavailable(result.Error!.Code);
        return null;
    }

    private async Task SendAsync(IReadOnlyList<WebSocketConnection> targets, EventMessage message)
    {
        if (targets.Count == 0)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes<Message>(message, ProtocolJsonContext.Default.Message);
        await Task.WhenAll(targets.Select(async target =>
        {
            try
            {
                await target.SendAsync(bytes).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                LogSendFailed(message.GetType().Name);
            }
        })).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not push {EventType} to a closing connection.")]
    private partial void LogSendFailed(string eventType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped an event whose payload could not be loaded ({Code}).")]
    private partial void LogPayloadUnavailable(DomainErrorCode code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No transport route for {EventType}.")]
    private partial void LogUnhandled(string eventType);
}
