using LeverDeck.Application.Common;
using LeverDeck.Application.Events;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Profiles;

/// <summary>
/// Command (admin scope): deletes a profile; the last one cannot be deleted. Buttons in other profiles that switch to
/// it are left as they are: they dangle, and the client ignores a switch to a profile that no longer exists.
/// </summary>
public sealed class DeleteProfile(IProfileRepository profiles, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, ProfileId profileId, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!stored.Any(profile => profile.Id == profileId))
            {
                return UseCase.Fail(DomainErrorCode.ProfileNotFound, $"Profile {profileId} does not exist.");
            }

            if (stored.Count == 1)
            {
                return UseCase.Fail(DomainErrorCode.LastProfileCannotBeDeleted, "The last profile cannot be deleted.");
            }

            await profiles.DeleteAsync(profileId, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new ProfileDeleted(profileId), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
