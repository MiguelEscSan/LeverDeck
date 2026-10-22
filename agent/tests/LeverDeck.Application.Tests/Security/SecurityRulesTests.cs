using System.Reflection;
using LeverDeck.Application.Actions;
using LeverDeck.Application.Pairing;
using LeverDeck.Application.Security;
using LeverDeck.Application.Tests.Fakes;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Pairing;
using LeverDeck.Domain.Profiles;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Application.Tests.Security;

/// <summary>The security rules listed in ROADMAP 2.4, each as an explicit test.</summary>
public sealed class SecurityRulesTests
{
    private const string Code = "100001";

    private readonly Harness _harness = new();

    private Task<Result> StartPairing() => _harness.StartPairing().ExecuteAsync(Caller.Admin, CancellationToken.None);

    private Task<Result<PairedDevice>> Pair(string code) =>
        _harness.PairDevice().ExecuteAsync(Caller.Public, code, "Phone", CancellationToken.None);

    [Fact]
    public async Task ConnectDevice_RevokedDevice_IsRejected()
    {
        await StartPairing();
        var paired = (await Pair(Code)).Value;
        await _harness.RevokeDevice().ExecuteAsync(Caller.Admin, paired.DeviceId, CancellationToken.None);

        var result = await _harness.ConnectDevice()
            .ExecuteAsync(Caller.Public, paired.RawToken, ApplicationInfo.ProtocolVersion, CancellationToken.None);

        Build.Fails(DomainErrorCode.DeviceRevoked, result);
    }

    [Fact]
    public async Task PairDevice_ExpiredCode_IsRejected()
    {
        await StartPairing();
        _harness.Clock.Advance(PairingSession.Lifetime);

        Build.Fails(DomainErrorCode.PairingCodeExpired, await Pair(Code));
        Assert.Empty(_harness.Devices.All);
    }

    [Fact]
    public async Task PairDevice_ReusedCode_IsRejected()
    {
        await StartPairing();
        await Pair(Code);

        Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair(Code));
        Assert.Single(_harness.Devices.All);
    }

    [Fact]
    public async Task PairDevice_RightCodeAfterFiveWrongGuesses_IsRejected()
    {
        await StartPairing();
        for (var guess = 0; guess < PairingSession.MaxFailedAttempts; guess++)
        {
            Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair($"99999{guess}"));
        }

        Build.Fails(DomainErrorCode.PairingCodeInvalid, await Pair(Code));
        Assert.Empty(_harness.Devices.All);
    }

    [Fact]
    public async Task PairDevice_ExhaustedSession_IsNoLongerOffered()
    {
        await StartPairing();
        for (var guess = 0; guess < PairingSession.MaxFailedAttempts; guess++)
        {
            await Pair("999999");
        }

        var session = await _harness.GetPairingSession().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Null(session.Value);
    }

    [Fact]
    public void ExecuteAction_Input_CannotCarryAnAction()
    {
        var parameters = typeof(ExecuteAction).GetMethod(nameof(ExecuteAction.ExecuteAsync))!.GetParameters();

        Assert.DoesNotContain(parameters, parameter => typeof(DomainAction).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(string));
    }

    [Fact]
    public async Task ExecuteAction_StoredActionChanged_RunsOnlyTheCurrentlyStoredAction()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", Build.Copy));
        var button = Build.ButtonAt(profile, 0, 0);
        await _harness.ExecuteAction().ExecuteAsync(Build.DeviceCaller, profile.Id, 0, button, CancellationToken.None);

        profile.ChangeButtonAction(button, Build.PlayPause);
        await _harness.ExecuteAction().ExecuteAsync(Build.DeviceCaller, profile.Id, 0, button, CancellationToken.None);

        Assert.Equal<DomainAction>([Build.Copy, Build.PlayPause], _harness.Executor.Executed);
    }

    [Fact]
    public async Task ExecuteAction_ClientSideAction_NeverReachesTheExecutor()
    {
        var profile = _harness.Profiles.Add(Build.ProfileWith("Main", new ChangePage(PageTarget.Previous)));

        var result = await _harness.ExecuteAction()
            .ExecuteAsync(Build.DeviceCaller, profile.Id, 0, Build.ButtonAt(profile, 0, 0), CancellationToken.None);

        Build.Fails(DomainErrorCode.ActionNotExecutable, result);
        Assert.Empty(_harness.Executor.Executed);
    }

    [Fact]
    public async Task AdminUseCases_FromDeviceScope_AreUnreachable()
    {
        await _harness.StartPairing().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);
        var other = _harness.Devices.Add(Build.DeviceWithToken("other", FakeClock.Start));
        var revoke = await _harness.RevokeDevice().ExecuteAsync(Build.DeviceCaller, other.Id, CancellationToken.None);

        Build.Fails(DomainErrorCode.Forbidden, revoke);
        Assert.False(other.IsRevoked);
        Assert.Null(_harness.Pairing.Current);
    }

    [Fact]
    public void UseCases_ArePublicSealedClassesWithOneExecuteAsyncMethod()
    {
        var useCases = typeof(ApplicationInfo).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && type.GetMethod("ExecuteAsync") is not null)
            .ToList();

        Assert.NotEmpty(useCases);
        Assert.All(useCases, type =>
        {
            Assert.True(type.IsSealed, $"{type.Name} must be sealed.");
            var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Equal(["ExecuteAsync"], publicMethods.Select(method => method.Name));
            Assert.Equal(typeof(Caller), publicMethods[0].GetParameters()[0].ParameterType);
            Assert.Equal(typeof(CancellationToken), publicMethods[0].GetParameters()[^1].ParameterType);
        });
    }
}
