using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>The profile seeded on first run, so a freshly paired device has something to press.</summary>
internal static class DefaultProfile
{
    public static Profile Create()
    {
        var profile = Profile.Create(new ProfileName("Default"), KeyColor.Teal, GridSize.FiveByThree);
        Place(profile, 0, 1, "Previous", "skip-previous", MediaKeyKind.Previous);
        Place(profile, 0, 2, "Play/Pause", "play-pause", MediaKeyKind.PlayPause);
        Place(profile, 0, 3, "Next", "skip-next", MediaKeyKind.Next);
        Place(profile, 1, 1, "Vol -", "volume-down", MediaKeyKind.VolumeDown);
        Place(profile, 1, 2, "Mute", "volume-mute", MediaKeyKind.Mute);
        Place(profile, 1, 3, "Vol +", "volume-up", MediaKeyKind.VolumeUp);
        return profile;
    }

    private static void Place(Profile profile, int row, int column, string label, string icon, MediaKeyKind key) =>
        profile.PlaceButton(
            0,
            new GridPosition(row, column),
            new ButtonAppearance(label, KeyColor.Dark, new IconId(icon)),
            new MediaKey(key));
}
