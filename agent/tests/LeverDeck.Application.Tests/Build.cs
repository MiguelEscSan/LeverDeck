using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Application.Tests;

/// <summary>Test data builders and shared assertions.</summary>
internal static class Build
{
    public static ButtonAppearance Appearance { get; } = new(string.Empty, KeyColor.Dark, new IconId("key"));

    public static Hotkey Copy { get; } = new(KeyModifiers.Ctrl, VirtualKey.C);

    public static MediaKey PlayPause { get; } = new(MediaKeyKind.PlayPause);

    public static Caller DeviceCaller { get; } = Caller.ForDevice(DeviceId.New());

    /// <summary>A 5×3 profile whose first page holds one button per action, left to right.</summary>
    public static Profile ProfileWith(string name, params DomainAction[] actions)
    {
        var profile = Profile.Create(new ProfileName(name), KeyColor.Blue, GridSize.FiveByThree);
        for (var column = 0; column < actions.Length; column++)
        {
            profile.PlaceButton(0, new GridPosition(0, column), Appearance, actions[column]);
        }

        return profile;
    }

    public static ButtonId ButtonAt(Profile profile, int pageIndex, int column) =>
        profile.Pages[pageIndex].ButtonAt(new GridPosition(0, column))!.Id;

    public static Device DeviceWithToken(string rawToken, DateTimeOffset at) =>
        Device.Pair(new DeviceName("Phone"), new TokenHash($"hash:{rawToken}"), at);

    public static void Fails(DomainErrorCode expected, Result result)
    {
        Assert.True(result.IsFailure, $"Expected {expected} but the call succeeded.");
        Assert.Equal(expected, result.Error!.Code);
    }
}
