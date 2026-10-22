using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Devices.Ports;
using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Pairing;

/// <summary>What a device receives once when it pairs. The raw token is never stored and never shown again.</summary>
public sealed record PairedDevice(DeviceId DeviceId, string RawToken)
{
    /// <summary>Redacted so the token never ends up in logs.</summary>
    public override string ToString() => $"PairedDevice {{ DeviceId = {DeviceId}, RawToken = *** }}";
}

/// <summary>
/// Command (public scope): redeems the pairing code and creates the device. Documented exception to "commands return
/// only an ack": it returns the <see cref="PairedDevice"/>, because the raw token can be delivered only once.
/// The device name is validated before the code is redeemed, so a bad name never burns the session. A failed
/// redemption keeps the session in the store, so wrong guesses add up to <see cref="PairingSession.MaxFailedAttempts"/>.
/// </summary>
public sealed class PairDevice(
    IPairingSessionStore store,
    IDeviceRepository devices,
    ITokenGenerator tokens,
    ITokenHasher hasher,
    IClock clock,
    IEventPublisher events)
{
    public Task<Result<PairedDevice>> ExecuteAsync(
        Caller caller,
        string code,
        string deviceName,
        CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Public, async () =>
        {
            var name = new DeviceName(deviceName);
            if (!TryParseCode(code, out var candidate) || store.Current is not { } session)
            {
                return UseCase.Fail<PairedDevice>(DomainErrorCode.PairingCodeInvalid, "Pairing code is invalid.");
            }

            var now = clock.UtcNow;
            var redeemed = session.Redeem(candidate, now);
            if (redeemed.IsFailure)
            {
                return UseCase.Fail<PairedDevice>(redeemed);
            }

            var rawToken = tokens.NewDeviceToken();
            var device = Device.Pair(name, hasher.Hash(rawToken), now);
            await devices.SaveAsync(device, cancellationToken).ConfigureAwait(false);
            store.Clear();
            await events.PublishAsync(new DevicesChanged(), cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new PairingSessionChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success(new PairedDevice(device.Id, rawToken));
        });

    private static bool TryParseCode(string code, out PairingCode candidate)
    {
        try
        {
            candidate = new PairingCode(code);
            return true;
        }
        catch (DomainException)
        {
            candidate = null!;
            return false;
        }
    }
}
