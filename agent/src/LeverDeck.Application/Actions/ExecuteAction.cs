using LeverDeck.Application.Common;
using LeverDeck.Application.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Actions;

/// <summary>
/// Command (device scope): runs the action of a stored button. The client names only the button; the action always
/// comes from the stored profile, so a client can never make the agent run anything that is not configured.
/// </summary>
public sealed class ExecuteAction(IProfileRepository profiles, ISettingsRepository settings, IActionExecutor executor)
{
    public Task<Result> ExecuteAsync(
        Caller caller,
        ProfileId profileId,
        int pageIndex,
        ButtonId buttonId,
        CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, async () =>
        {
            var profile = await profiles.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                return UseCase.Fail(DomainErrorCode.ProfileNotFound, $"Profile {profileId} does not exist.");
            }

            if (pageIndex < 0 || pageIndex >= profile.Pages.Count)
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Page {pageIndex} does not exist.");
            }

            var button = profile.Pages[pageIndex].Buttons.FirstOrDefault(candidate => candidate.Id == buttonId);
            if (button is null)
            {
                return UseCase.Fail(DomainErrorCode.ButtonNotFound, $"Button {buttonId} is not on page {pageIndex}.");
            }

            var action = button.Action;
            if (action.Target == ExecutionTarget.Client)
            {
                return UseCase.Fail(DomainErrorCode.ActionNotExecutable, "This action runs on the client.");
            }

            if (action is LaunchApp launch)
            {
                var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
                if (current.FindApp(launch.App) is null)
                {
                    return UseCase.Fail(DomainErrorCode.ActionNotExecutable, $"App {launch.App} is not registered.");
                }
            }

            return await executor.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
        });
}
