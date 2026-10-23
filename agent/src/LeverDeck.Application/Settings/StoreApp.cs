namespace LeverDeck.Application.Settings;

/// <summary>A Start menu entry as the shell reports it: its display name and its AppUserModelId (<c>PackageFamilyName!AppId</c>).</summary>
public sealed record StoreApp(string Name, string AppUserModelId);
