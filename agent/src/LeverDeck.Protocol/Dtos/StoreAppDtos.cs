namespace LeverDeck.Protocol.Dtos;

/// <summary>A Microsoft Store app the editor can register: its name and its AppUserModelId (<c>PackageFamilyName!AppId</c>).</summary>
public sealed record StoreAppDto(string Name, string AppUserModelId);
