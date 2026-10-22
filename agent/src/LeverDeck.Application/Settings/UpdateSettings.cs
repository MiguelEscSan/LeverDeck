using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;

namespace LeverDeck.Application.Settings;

/// <summary>
/// Command (admin scope): replaces the general settings. A <see langword="null"/> OBS connection clears it.
/// Registered apps are managed by <see cref="RegisterApp"/> and <see cref="UnregisterApp"/> and are kept as they are.
/// </summary>
public sealed class UpdateSettings(ISettingsRepository settings, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(
        Caller caller,
        bool startWithWindows,
        GridSize defaultGridSize,
        ObsConnection? obsConnection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaultGridSize);
        return UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
            current.SetStartWithWindows(startWithWindows);
            current.ChangeDefaultGridSize(defaultGridSize);
            current.ConfigureObs(obsConnection);
            await settings.SaveAsync(current, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SettingsUpdated(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
