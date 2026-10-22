using LeverDeck.Application.Devices;
using LeverDeck.Application.Icons;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Profiles;
using LeverDeck.Application.Security;
using LeverDeck.Application.Settings;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// Payloads that both query results and pushed events carry, built through the query use cases so both paths apply
/// the same scope rules. Adds what only the transport knows: which devices are connected and the pairing links.
/// </summary>
public sealed class TransportPayloads(
    GetProfile getProfile,
    GetDevices getDevices,
    GetPairingSession getPairingSession,
    GetSettings getSettings,
    GetIcons getIcons,
    ConnectionRegistry registry,
    PairingLinks links)
{
    public async Task<Result<ProfilePayload>> ProfileAsync(Caller caller, ProfileId profileId, CancellationToken cancellationToken) =>
        Map(
            await getProfile.ExecuteAsync(caller, profileId, cancellationToken).ConfigureAwait(false),
            profile => new ProfilePayload(ProfileMapper.ToDto(profile)));

    public async Task<Result<DevicesPayload>> DevicesAsync(Caller caller, CancellationToken cancellationToken) =>
        Map(
            await getDevices.ExecuteAsync(caller, cancellationToken).ConfigureAwait(false),
            devices => new DevicesPayload([.. devices.Select(device => DeviceMapper.ToDto(device, registry.IsConnected(device.Id)))]));

    public async Task<Result<PairingSessionPayload>> PairingSessionAsync(Caller caller, CancellationToken cancellationToken) =>
        Map(
            await getPairingSession.ExecuteAsync(caller, cancellationToken).ConfigureAwait(false),
            session => new PairingSessionPayload(session is null
                ? null
                : PairingSessionMapper.ToDto(session, PairingLinks.QrUrl(session.Code), links.FallbackUrl(session.Code))));

    public async Task<Result<SettingsPayload>> SettingsAsync(Caller caller, CancellationToken cancellationToken) =>
        Map(
            await getSettings.ExecuteAsync(caller, cancellationToken).ConfigureAwait(false),
            settings => new SettingsPayload(SettingsMapper.ToDto(settings)));

    public async Task<Result<IconsPayload>> IconsAsync(Caller caller, CancellationToken cancellationToken) =>
        Map(
            await getIcons.ExecuteAsync(caller, cancellationToken).ConfigureAwait(false),
            icons => new IconsPayload([.. icons.Select(icon => icon.Value)]));

    private static Result<TOut> Map<TIn, TOut>(Result<TIn> result, Func<TIn, TOut> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error!);
}
