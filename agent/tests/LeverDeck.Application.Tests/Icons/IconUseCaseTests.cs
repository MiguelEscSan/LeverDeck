using System.Buffers.Binary;
using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Tests.Icons;

public sealed class IconUseCaseTests
{
    private static readonly IconId _icon = new("custom-0123456789abcdef0123456789abcdef");
    private static readonly IconId _other = new("custom-fedcba9876543210fedcba9876543210");

    private readonly Harness _harness = new();

    internal static IconImage Image(int side = 256)
    {
        var bytes = new byte[37];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), side);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), side);
        return new IconImage(bytes);
    }

    // SaveIcon

    [Fact]
    public async Task SaveIcon_NewCustomId_StoresItAndPublishesIconsChanged()
    {
        var result = await _harness.SaveIcon().ExecuteAsync(Caller.Admin, _icon, Image(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_harness.Icons.Stored.ContainsKey(_icon));
        Assert.Equal([new IconsChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task SaveIcon_BundledIconId_FailsWithValidationFailed()
    {
        var result = await _harness.SaveIcon().ExecuteAsync(Caller.Admin, new IconId("play-pause"), Image(), CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Empty(_harness.Icons.Stored);
    }

    [Fact]
    public async Task SaveIcon_ExistingId_IsNeverOverwritten()
    {
        var original = Image(128);
        _harness.Icons.Stored[_icon] = original;

        var result = await _harness.SaveIcon().ExecuteAsync(Caller.Admin, _icon, Image(64), CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Same(original, _harness.Icons.Stored[_icon]);
        Assert.Empty(_harness.Events.Published);
    }

    // DeleteIcon

    [Fact]
    public async Task DeleteIcon_UnusedIcon_DeletesItAndPublishesIconsChanged()
    {
        _harness.Icons.Stored[_icon] = Image();

        var result = await _harness.DeleteIcon().ExecuteAsync(Caller.Admin, _icon, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_harness.Icons.Stored);
        Assert.Equal([new IconsChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task DeleteIcon_UnknownIcon_FailsWithIconNotFound()
    {
        var result = await _harness.DeleteIcon().ExecuteAsync(Caller.Admin, _icon, CancellationToken.None);

        Build.Fails(DomainErrorCode.IconNotFound, result);
    }

    [Fact]
    public async Task DeleteIcon_IconShownByAKey_FailsWithValidationFailed()
    {
        _harness.Icons.Stored[_icon] = Image();
        var profile = Build.ProfileWith("Main");
        profile.PlaceButton(0, new GridPosition(0, 0), new ButtonAppearance("Steam", KeyColor.Dark, _icon), new MediaKey(MediaKeyKind.Mute));
        _harness.Profiles.Add(profile);

        var result = await _harness.DeleteIcon().ExecuteAsync(Caller.Admin, _icon, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.True(_harness.Icons.Stored.ContainsKey(_icon));
        Assert.Empty(_harness.Events.Published);
    }

    // GetIcons

    [Fact]
    public async Task GetIcons_ListsTheUploadedIconsById()
    {
        _harness.Icons.Stored[_other] = Image();
        _harness.Icons.Stored[_icon] = Image();

        var result = await _harness.GetIcons().ExecuteAsync(Caller.Admin, CancellationToken.None);

        Assert.Equal([_icon, _other], result.Value);
        Assert.Equal(0, _harness.RepositoryWrites);
    }
}
