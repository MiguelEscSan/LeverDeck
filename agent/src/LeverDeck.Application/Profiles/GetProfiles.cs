using LeverDeck.Application.Common;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Profiles;

/// <summary>What a device needs to list profiles without loading their pages.</summary>
public sealed record ProfileSummary(ProfileId Id, ProfileName Name, KeyColor Color);

/// <summary>Query (device scope): every profile as a summary, in the repository's order.</summary>
public sealed class GetProfiles(IProfileRepository profiles)
{
    public Task<Result<IReadOnlyList<ProfileSummary>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, async () =>
        {
            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ProfileSummary> summaries =
                [.. stored.Select(profile => new ProfileSummary(profile.Id, profile.Name, profile.Color))];
            return Result.Success(summaries);
        });
}
