using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly List<Profile> _profiles = [];

    public int Writes { get; private set; }

    public IReadOnlyList<Profile> All => _profiles;

    /// <summary>Seeds a profile without counting it as a write.</summary>
    public Profile Add(Profile profile)
    {
        _profiles.Add(profile);
        return profile;
    }

    public Task<Profile?> GetByIdAsync(ProfileId id, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.Find(profile => profile.Id == id));

    public Task<IReadOnlyList<Profile>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Profile>>([.. _profiles]);

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken)
    {
        Writes++;
        var index = _profiles.FindIndex(existing => existing.Id == profile.Id);
        if (index >= 0)
        {
            _profiles[index] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(ProfileId id, CancellationToken cancellationToken)
    {
        Writes++;
        _profiles.RemoveAll(profile => profile.Id == id);
        return Task.CompletedTask;
    }
}
