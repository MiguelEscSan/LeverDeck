using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Profiles;

public sealed class DeleteProfileTests
{
    private readonly Harness _harness = new();

    private Task<Result> Delete(ProfileId id) =>
        _harness.DeleteProfile().ExecuteAsync(Caller.Admin, id, CancellationToken.None);

    [Fact]
    public async Task ExecuteAsync_OneOfSeveral_DeletesItAndPublishesProfileDeleted()
    {
        var kept = _harness.Profiles.Add(Build.ProfileWith("Main"));
        var deleted = _harness.Profiles.Add(Build.ProfileWith("Gaming"));

        var result = await Delete(deleted.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(kept, Assert.Single(_harness.Profiles.All));
        Assert.Equal([new ProfileDeleted(deleted.Id)], _harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProfile_FailsWithProfileNotFound()
    {
        _harness.Profiles.Add(Build.ProfileWith("Main"));
        _harness.Profiles.Add(Build.ProfileWith("Gaming"));

        Build.Fails(DomainErrorCode.ProfileNotFound, await Delete(ProfileId.New()));
        Assert.Equal(0, _harness.RepositoryWrites);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_LastProfile_FailsWithLastProfileCannotBeDeleted()
    {
        var only = _harness.Profiles.Add(Build.ProfileWith("Main"));

        Build.Fails(DomainErrorCode.LastProfileCannotBeDeleted, await Delete(only.Id));
        Assert.Single(_harness.Profiles.All);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task ExecuteAsync_ProfileTargetedBySwitchElsewhere_IsDeletedAndLeavesTheSwitchDangling()
    {
        var deleted = _harness.Profiles.Add(Build.ProfileWith("Gaming"));
        var other = _harness.Profiles.Add(Build.ProfileWith("Main", new SwitchProfile(deleted.Id)));

        var result = await Delete(deleted.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new SwitchProfile(deleted.Id), Assert.Single(other.Pages[0].Buttons).Action);
    }
}
