namespace LeverDeck.Domain.Actions;

/// <summary>The whitelist: every action type that may exist. Architecture tests keep it in sync with the hierarchy.</summary>
public static class ActionCatalog
{
    public static IReadOnlyList<Type> All { get; } =
    [
        typeof(Hotkey),
        typeof(MediaKey),
        typeof(LaunchApp),
        typeof(OpenUrl),
        typeof(ObsScene),
        typeof(LaunchGame),
        typeof(ChangePage),
        typeof(SwitchProfile),
    ];
}
