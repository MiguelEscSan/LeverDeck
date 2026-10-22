using LeverDeck.Application.Actions;
using LeverDeck.Application.Devices;
using LeverDeck.Application.Games;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Profiles;
using LeverDeck.Application.Security;
using LeverDeck.Application.Settings;
using LeverDeck.Application.Status;
using LeverDeck.Domain.Common;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// Routes one command or query to its use case as the connection's caller and builds the answer with the same request
/// id: an ack or error for commands, a query result or error for queries. Scope is enforced by the use cases, so a
/// device asking for an admin operation gets <c>forbidden</c> from them. An unexpected failure is logged and answered
/// with <c>internal</c>; it never ends the connection.
/// </summary>
public sealed partial class MessageRouter(
    ExecuteAction executeAction,
    SaveProfile saveProfile,
    DeleteProfile deleteProfile,
    GetProfiles getProfiles,
    StartPairing startPairing,
    CancelPairing cancelPairing,
    RenameDevice renameDevice,
    RevokeDevice revokeDevice,
    GetSettings getSettings,
    UpdateSettings updateSettings,
    RegisterApp registerApp,
    UnregisterApp unregisterApp,
    GetAgentStatus getAgentStatus,
    GetInstalledGames getInstalledGames,
    TransportPayloads payloads,
    ILogger<MessageRouter> logger)
{
    public async Task<Message> HandleAsync(Caller caller, Message request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var requestId = request switch
        {
            CommandMessage command => command.RequestId,
            QueryMessage query => query.RequestId,
            _ => null,
        };

        try
        {
            return request switch
            {
                CommandMessage command => await HandleCommandAsync(caller, command, cancellationToken).ConfigureAwait(false),
                QueryMessage query => await HandleQueryAsync(caller, query, cancellationToken).ConfigureAwait(false),
                _ => Invalid(requestId, "Clients send only commands and queries."),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRequestFailed(exception, request.GetType().Name);
            return ErrorMapper.Internal(requestId);
        }
    }

    internal static ErrorMessage Invalid(string? requestId, string message) => new(requestId, ErrorCodeDto.ValidationFailed, message);

    private async Task<Message> HandleCommandAsync(Caller caller, CommandMessage command, CancellationToken ct)
    {
        var id = command.RequestId;
        switch (command)
        {
            case ConnectDeviceCommand:
                return Invalid(id, "connectDevice is valid only as the first message on /ws.");

            case ExecuteActionCommand execute:
            {
                var profileId = IdMapper.ToProfileId(execute.Payload.ProfileId);
                var buttonId = IdMapper.ToButtonId(execute.Payload.ButtonId);
                if (profileId.IsFailure || buttonId.IsFailure)
                {
                    return ErrorMapper.ToMessage(id, (profileId.Error ?? buttonId.Error)!);
                }

                return Ack(id, await executeAction.ExecuteAsync(caller, profileId.Value, execute.Payload.PageIndex, buttonId.Value, ct).ConfigureAwait(false));
            }

            case SaveProfileCommand save:
                return await BindAsync(id, ProfileMapper.ToDomain(save.Payload.Profile), async profile =>
                    Ack(id, await saveProfile.ExecuteAsync(caller, profile, ct).ConfigureAwait(false))).ConfigureAwait(false);

            case DeleteProfileCommand delete:
                return await BindAsync(id, IdMapper.ToProfileId(delete.Payload.ProfileId), async profileId =>
                    Ack(id, await deleteProfile.ExecuteAsync(caller, profileId, ct).ConfigureAwait(false))).ConfigureAwait(false);

            case StartPairingCommand:
                return Ack(id, await startPairing.ExecuteAsync(caller, ct).ConfigureAwait(false));

            case CancelPairingCommand:
                return Ack(id, await cancelPairing.ExecuteAsync(caller, ct).ConfigureAwait(false));

            case RenameDeviceCommand rename:
                return await BindAsync(id, IdMapper.ToDeviceId(rename.Payload.DeviceId), async deviceId =>
                    Ack(id, await renameDevice.ExecuteAsync(caller, deviceId, rename.Payload.Name, ct).ConfigureAwait(false))).ConfigureAwait(false);

            case RevokeDeviceCommand revoke:
                return await BindAsync(id, IdMapper.ToDeviceId(revoke.Payload.DeviceId), async deviceId =>
                    Ack(id, await revokeDevice.ExecuteAsync(caller, deviceId, ct).ConfigureAwait(false))).ConfigureAwait(false);

            case UpdateSettingsCommand update:
            {
                // The current settings are read as the caller, so a device is refused here already.
                var current = await getSettings.ExecuteAsync(caller, ct).ConfigureAwait(false);
                if (current.IsFailure)
                {
                    return ErrorMapper.ToMessage(id, current.Error!);
                }

                return await BindAsync(id, SettingsMapper.ToUpdate(update.Payload.Settings, current.Value.ObsConnection), async settings =>
                    Ack(id, await updateSettings.ExecuteAsync(caller, settings.StartWithWindows, settings.DefaultGridSize, settings.ObsConnection, ct)
                        .ConfigureAwait(false))).ConfigureAwait(false);
            }

            case RegisterAppCommand register:
                return ResponseMapper.ToResponse(
                    id,
                    await registerApp.ExecuteAsync(caller, register.Payload.DisplayName, register.Payload.Path, ct).ConfigureAwait(false),
                    appId => new AckMessage(id, new AppRegisteredPayload(appId.Value)));

            case UnregisterAppCommand unregister:
                return await BindAsync(id, IdMapper.ToAppId(unregister.Payload.AppId), async appId =>
                    Ack(id, await unregisterApp.ExecuteAsync(caller, appId, ct).ConfigureAwait(false))).ConfigureAwait(false);

            default:
                return Invalid(id, $"Unknown command {command.GetType().Name}.");
        }
    }

    private async Task<Message> HandleQueryAsync(Caller caller, QueryMessage query, CancellationToken ct)
    {
        var id = query.RequestId;
        switch (query)
        {
            case GetProfilesQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await getProfiles.ExecuteAsync(caller, ct).ConfigureAwait(false),
                    summaries => new ProfilesResult(id, new ProfilesPayload([.. summaries.Select(ProfileMapper.ToSummaryDto)])));

            case GetProfileQuery get:
                return await BindAsync(id, IdMapper.ToProfileId(get.Payload.ProfileId), async profileId =>
                    ResponseMapper.ToResponse(
                        id,
                        await payloads.ProfileAsync(caller, profileId, ct).ConfigureAwait(false),
                        payload => new ProfileResult(id, payload))).ConfigureAwait(false);

            case GetDevicesQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await payloads.DevicesAsync(caller, ct).ConfigureAwait(false),
                    payload => new DevicesResult(id, payload));

            case GetPairingSessionQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await payloads.PairingSessionAsync(caller, ct).ConfigureAwait(false),
                    payload => new PairingSessionResult(id, payload));

            case GetSettingsQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await payloads.SettingsAsync(caller, ct).ConfigureAwait(false),
                    payload => new SettingsResult(id, payload));

            case GetAgentStatusQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await getAgentStatus.ExecuteAsync(caller, ct).ConfigureAwait(false),
                    status => new AgentStatusResult(id, StatusMapper.ToPayload(status, obsConnected: false)));

            case GetInstalledGamesQuery:
                return ResponseMapper.ToResponse(
                    id,
                    await getInstalledGames.ExecuteAsync(caller, ct).ConfigureAwait(false),
                    games => new InstalledGamesResult(id, new InstalledGamesPayload([.. games.Select(GameMapper.ToDto)])));

            default:
                return Invalid(id, $"Unknown query {query.GetType().Name}.");
        }
    }

    private static Message Ack(string requestId, Result result) => ResponseMapper.ToResponse(requestId, result);

    /// <summary>Runs <paramref name="next"/> with a mapped value, or answers the mapping failure.</summary>
    private static async Task<Message> BindAsync<T>(string requestId, Result<T> mapped, Func<T, Task<Message>> next) =>
        mapped.IsSuccess ? await next(mapped.Value).ConfigureAwait(false) : ErrorMapper.ToMessage(requestId, mapped.Error!);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling {RequestType} failed unexpectedly.")]
    private partial void LogRequestFailed(Exception exception, string requestType);
}
