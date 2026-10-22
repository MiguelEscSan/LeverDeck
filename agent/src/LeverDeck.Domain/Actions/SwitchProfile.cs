using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Actions;

/// <summary>Makes the client show another profile. Pointing at the button's own profile is a no-op.</summary>
public sealed record SwitchProfile : Action
{
    public SwitchProfile(ProfileId profile)
    {
        Invariant.Require(profile.Value != Guid.Empty, DomainErrorCode.ValidationFailed, "Profile id must not be empty.");
        Profile = profile;
    }

    public ProfileId Profile { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Client;
}
