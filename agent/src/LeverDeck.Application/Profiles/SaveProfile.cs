using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Profiles;

/// <summary>
/// Command (admin scope): creates or replaces a profile. The profile's own invariants were checked when it was built;
/// this adds the cross-aggregate rules: a name unique across profiles (ignoring case), every
/// <see cref="LaunchApp"/> pointing to a registered app and every <see cref="SwitchProfile"/> pointing to itself or
/// to a stored profile.
/// </summary>
public sealed class SaveProfile(IProfileRepository profiles, ISettingsRepository settings, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, Profile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            var others = stored.Where(other => other.Id != profile.Id).ToList();
            if (others.Any(other => string.Equals(other.Name.Value, profile.Name.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return UseCase.Fail(DomainErrorCode.DuplicateProfileName, $"A profile named '{profile.Name}' already exists.");
            }

            var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
            foreach (var action in profile.Pages.SelectMany(page => page.Buttons).Select(button => button.Action))
            {
                if (action is LaunchApp launch && current.FindApp(launch.App) is null)
                {
                    return UseCase.Fail(DomainErrorCode.ValidationFailed, $"App {launch.App} is not registered.");
                }

                if (action is SwitchProfile target
                    && target.Profile != profile.Id
                    && others.TrueForAll(other => other.Id != target.Profile))
                {
                    return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Profile {target.Profile} does not exist.");
                }
            }

            await profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new ProfileUpdated(profile.Id), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
