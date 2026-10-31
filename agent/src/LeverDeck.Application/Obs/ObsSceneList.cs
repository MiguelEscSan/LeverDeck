namespace LeverDeck.Application.Obs;

/// <summary>The scene names OBS reports; <see cref="CurrentScene"/> is null while OBS has no program scene.</summary>
public sealed record ObsSceneList(IReadOnlyList<string> Scenes, string? CurrentScene);
