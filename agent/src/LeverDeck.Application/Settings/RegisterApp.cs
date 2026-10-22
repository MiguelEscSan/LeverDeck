using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;

namespace LeverDeck.Application.Settings;

/// <summary>
/// Command (admin scope): registers an executable that <see cref="Domain.Actions.LaunchApp"/> buttons may launch.
/// Documented exception to "commands return only an ack": it returns the new <see cref="AppId"/>, which the agent
/// generates and the editor needs right away to point a button at the app.
/// </summary>
public sealed class RegisterApp(ISettingsRepository settings, IEventPublisher events)
{
    public Task<Result<AppId>> ExecuteAsync(
        Caller caller,
        string displayName,
        string executablePath,
        CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
            var appId = current.RegisterApp(displayName, executablePath);
            await settings.SaveAsync(current, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SettingsUpdated(), cancellationToken).ConfigureAwait(false);
            return Result.Success(appId);
        });
}
