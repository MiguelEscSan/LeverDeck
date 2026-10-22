using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Profiles.Ports;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(ProfileId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Profile>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates the profile or replaces the stored one with the same id.</summary>
    Task SaveAsync(Profile profile, CancellationToken cancellationToken);

    Task DeleteAsync(ProfileId id, CancellationToken cancellationToken);
}
