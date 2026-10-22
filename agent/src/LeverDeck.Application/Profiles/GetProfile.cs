using LeverDeck.Application.Common;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Profiles;

/// <summary>Query (device scope): one full profile.</summary>
public sealed class GetProfile(IProfileRepository profiles)
{
    public Task<Result<Profile>> ExecuteAsync(Caller caller, ProfileId profileId, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, async () =>
        {
            var profile = await profiles.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
            return profile is null
                ? UseCase.Fail<Profile>(DomainErrorCode.ProfileNotFound, $"Profile {profileId} does not exist.")
                : Result.Success(profile);
        });
}
