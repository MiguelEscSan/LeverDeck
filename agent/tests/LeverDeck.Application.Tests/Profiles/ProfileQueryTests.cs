using LeverDeck.Application.Profiles;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Profiles;

public sealed class ProfileQueryTests
{
    private readonly Harness _harness = new();

    [Fact]
    public async Task GetProfiles_StoredProfiles_ReturnsSummariesInStoredOrder()
    {
        var main = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));
        var gaming = _harness.Profiles.Add(Build.ProfileWith("Gaming"));

        var result = await _harness.GetProfiles().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.Equal(
            [new ProfileSummary(main.Id, main.Name, KeyColor.Blue), new ProfileSummary(gaming.Id, gaming.Name, KeyColor.Blue)],
            result.Value);
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task GetProfiles_NoProfiles_ReturnsEmptyList()
    {
        var result = await _harness.GetProfiles().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetProfile_StoredProfile_ReturnsIt()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main"));

        var result = await _harness.GetProfile().ExecuteAsync(Build.DeviceCaller, profile.Id, CancellationToken.None);

        Assert.Same(profile, result.Value);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task GetProfile_UnknownProfile_FailsWithProfileNotFound()
    {
        var result = await _harness.GetProfile().ExecuteAsync(Build.DeviceCaller, ProfileId.New(), CancellationToken.None);

        Build.Fails(DomainErrorCode.ProfileNotFound, result);
    }
}
