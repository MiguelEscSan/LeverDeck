using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Application.Settings;

/// <summary>Command (admin scope): unregisters an app. An app still launched by any button is rejected.</summary>
public sealed class UnregisterApp(ISettingsRepository settings, IProfileRepository profiles, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, AppId appId, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            var inUse = stored
                .SelectMany(profile => profile.Pages)
                .SelectMany(page => page.Buttons)
                .Any(button => button.Action is LaunchApp launch && launch.App == appId);
            if (inUse)
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"App {appId} is still used by a button.");
            }

            var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
            current.UnregisterApp(appId);
            await settings.SaveAsync(current, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SettingsUpdated(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
