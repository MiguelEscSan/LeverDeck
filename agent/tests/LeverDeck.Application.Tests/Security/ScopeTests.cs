using LeverDeck.Application.Security;
using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Security;

/// <summary>
/// Every use case against every caller scope. The rule: a caller may run a use case at or below its own scope;
/// a public caller asking for more gets <see cref="DomainErrorCode.Unauthorized"/>, a device asking for admin gets
/// <see cref="DomainErrorCode.Forbidden"/>. A denied call has no side effect.
/// </summary>
public sealed class ScopeTests
{
    private static readonly Dictionary<string, (CallerScope Scope, Func<Harness, Caller, Task<Result>> Run)> UseCases = new()
    {
        ["ConnectDevice"] = (CallerScope.Public, async (h, c) =>
            await h.ConnectDevice().ExecuteAsync(c, "token", ApplicationInfo.ProtocolVersion, CancellationToken.None)),
        ["PairDevice"] = (CallerScope.Public, async (h, c) =>
            await h.PairDevice().ExecuteAsync(c, "123456", "Phone", CancellationToken.None)),
        ["ExecuteAction"] = (CallerScope.Device, (h, c) =>
            h.ExecuteAction().ExecuteAsync(c, ProfileId.New(), 0, ButtonId.New(), CancellationToken.None)),
        ["GetProfiles"] = (CallerScope.Device, async (h, c) =>
            await h.GetProfiles().ExecuteAsync(c, CancellationToken.None)),
        ["GetProfile"] = (CallerScope.Device, async (h, c) =>
            await h.GetProfile().ExecuteAsync(c, ProfileId.New(), CancellationToken.None)),
        ["GetAgentStatus"] = (CallerScope.Device, async (h, c) =>
            await Harness.GetAgentStatus().ExecuteAsync(c, CancellationToken.None)),
        ["SaveProfile"] = (CallerScope.Admin, (h, c) =>
            h.SaveProfile().ExecuteAsync(c, Build.ProfileWith("Main"), CancellationToken.None)),
        ["DeleteProfile"] = (CallerScope.Admin, (h, c) =>
            h.DeleteProfile().ExecuteAsync(c, ProfileId.New(), CancellationToken.None)),
        ["StartPairing"] = (CallerScope.Admin, (h, c) =>
            h.StartPairing().ExecuteAsync(c, CancellationToken.None)),
        ["CancelPairing"] = (CallerScope.Admin, (h, c) =>
            h.CancelPairing().ExecuteAsync(c, CancellationToken.None)),
        ["RenameDevice"] = (CallerScope.Admin, (h, c) =>
            h.RenameDevice().ExecuteAsync(c, DeviceId.New(), "Tablet", CancellationToken.None)),
        ["RevokeDevice"] = (CallerScope.Admin, (h, c) =>
            h.RevokeDevice().ExecuteAsync(c, DeviceId.New(), CancellationToken.None)),
        ["UpdateSettings"] = (CallerScope.Admin, (h, c) =>
            h.UpdateSettings().ExecuteAsync(c, true, GridSize.EightByFour, null, CancellationToken.None)),
        ["RegisterApp"] = (CallerScope.Admin, async (h, c) =>
            await h.RegisterApp().ExecuteAsync(c, "Notepad", @"C:\Windows\notepad.exe", CancellationToken.None)),
        ["UnregisterApp"] = (CallerScope.Admin, (h, c) =>
            h.UnregisterApp().ExecuteAsync(c, AppId.New(), CancellationToken.None)),
        ["GetDevices"] = (CallerScope.Admin, async (h, c) =>
            await h.GetDevices().ExecuteAsync(c, CancellationToken.None)),
        ["GetPairingSession"] = (CallerScope.Admin, async (h, c) =>
            await h.GetPairingSession().ExecuteAsync(c, CancellationToken.None)),
        ["GetSettings"] = (CallerScope.Admin, async (h, c) =>
            await h.GetSettings().ExecuteAsync(c, CancellationToken.None)),
        ["GetInstalledGames"] = (CallerScope.Admin, async (h, c) =>
            await h.GetInstalledGames().ExecuteAsync(c, CancellationToken.None)),
    };

    private static readonly Dictionary<CallerScope, Caller> Callers = new()
    {
        [CallerScope.Public] = Caller.Public,
        [CallerScope.Device] = Build.DeviceCaller,
        [CallerScope.Admin] = Caller.Admin,
    };

    public static TheoryData<string, CallerScope> DeniedCalls
    {
        get
        {
            var data = new TheoryData<string, CallerScope>();
            foreach (var (name, useCase) in UseCases)
            {
                foreach (var scope in Enum.GetValues<CallerScope>().Where(scope => scope < useCase.Scope))
                {
                    data.Add(name, scope);
                }
            }

            return data;
        }
    }

    public static TheoryData<string, CallerScope> AllowedCalls
    {
        get
        {
            var data = new TheoryData<string, CallerScope>();
            foreach (var (name, useCase) in UseCases)
            {
                foreach (var scope in Enum.GetValues<CallerScope>().Where(scope => scope >= useCase.Scope))
                {
                    data.Add(name, scope);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> AdminUseCases => [.. UseCases.Where(pair => pair.Value.Scope == CallerScope.Admin).Select(pair => pair.Key)];

    [Fact]
    public void UseCases_CoverEveryUseCaseInTheAssembly()
    {
        var useCaseTypes = typeof(ApplicationInfo).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && type.GetMethod("ExecuteAsync") is not null)
            .Select(type => type.Name);

        Assert.Equal(useCaseTypes.Order(), UseCases.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(DeniedCalls))]
    public async Task ExecuteAsync_CallerBelowRequiredScope_IsDeniedWithoutSideEffects(string useCase, CallerScope callerScope)
    {
        var harness = new Harness();
        var expected = callerScope == CallerScope.Public ? DomainErrorCode.Unauthorized : DomainErrorCode.Forbidden;

        var result = await UseCases[useCase].Run(harness, Callers[callerScope]);

        Build.Fails(expected, result);
        Assert.Equal(0, harness.RepositoryWrites);
        Assert.Null(harness.Pairing.Current);
        Assert.Empty(harness.Events.Published);
        Assert.Empty(harness.Executor.Executed);
    }

    [Theory]
    [MemberData(nameof(AllowedCalls))]
    public async Task ExecuteAsync_CallerAtOrAboveRequiredScope_PassesTheScopeCheck(string useCase, CallerScope callerScope)
    {
        var result = await UseCases[useCase].Run(new Harness(), Callers[callerScope]);

        Assert.False(
            result.Error?.Code is DomainErrorCode.Forbidden || (result.Error?.Code is DomainErrorCode.Unauthorized && useCase != "ConnectDevice"),
            $"{useCase} denied a {callerScope} caller: {result.Error}");
    }

    [Theory]
    [MemberData(nameof(AdminUseCases))]
    public async Task ExecuteAsync_AdminUseCaseFromDeviceScope_IsForbidden(string useCase)
    {
        var result = await UseCases[useCase].Run(new Harness(), Build.DeviceCaller);

        Build.Fails(DomainErrorCode.Forbidden, result);
    }

    [Fact]
    public void Caller_ForDevice_CarriesTheDeviceId()
    {
        var id = DeviceId.New();

        var caller = Caller.ForDevice(id);

        Assert.Equal(CallerScope.Device, caller.Scope);
        Assert.Equal(id, caller.DeviceId);
    }

    [Fact]
    public void Caller_PublicAndAdmin_CarryNoDeviceId()
    {
        Assert.Equal(CallerScope.Public, Caller.Public.Scope);
        Assert.Null(Caller.Public.DeviceId);
        Assert.Equal(CallerScope.Admin, Caller.Admin.Scope);
        Assert.Null(Caller.Admin.DeviceId);
    }

    [Fact]
    public async Task ExecuteAsync_NullCaller_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => UseCases["GetProfiles"].Run(new Harness(), null!));
    }
}
