using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Tests.Sounds;

public sealed class SoundUseCaseTests
{
    private static readonly SoundId _clip = new("sound-0123456789abcdef0123456789abcdef");
    private static readonly SoundId _other = new("sound-fedcba9876543210fedcba9876543210");

    private readonly Harness _harness = new();

    private static ReadOnlyMemory<byte> Wave(int dataBytes = 176400) => WaveBytes(dataBytes);

    internal static byte[] WaveBytes(int dataBytes)
    {
        // A minimal stereo 16-bit 44.1 kHz WAV: 176400 data bytes are one second.
        var header = new MemoryStream();
        using var writer = new BinaryWriter(header);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(44100);
        writer.Write(176400);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        writer.Write(new byte[dataBytes]);
        writer.Flush();
        return header.ToArray();
    }

    private static SoundName Name(string value) => new(value);

    // GetSounds

    [Fact]
    public async Task GetSounds_ListsTheClipsByName_ForAnAuthenticatedDevice()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Zebra"), 1000, 100);
        _harness.Sounds.Clips[_other] = new SoundClip(_other, Name("Applause"), 3000, 200);

        var result = await _harness.GetSounds().ExecuteAsync(Build.DeviceCaller, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Applause", "Zebra"], result.Value.Select(clip => clip.Name.Value));
    }

    [Fact]
    public async Task GetSounds_ForAnonymousCaller_IsUnauthorized()
    {
        var result = await _harness.GetSounds().ExecuteAsync(Caller.Public, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCode.Unauthorized, result.Error!.Code);
    }

    // UploadSound

    [Fact]
    public async Task UploadSound_ValidWave_StoresTheClipWithItsLengthAndPublishes()
    {
        var result = await _harness.UploadSound().ExecuteAsync(Caller.Admin, _clip, Name("Applause"), Wave(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var clip = _harness.Sounds.Clips[_clip];
        Assert.Equal("Applause", clip.Name.Value);
        Assert.Equal(1000, clip.DurationMs);
        Assert.Equal(Wave().Length, clip.SizeBytes);
        Assert.Equal([new SoundsChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task UploadSound_ExistingId_IsNeverOverwritten()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Original"), 500, 50);

        var result = await _harness.UploadSound().ExecuteAsync(Caller.Admin, _clip, Name("Replacement"), Wave(), CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Equal("Original", _harness.Sounds.Clips[_clip].Name.Value);
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task UploadSound_NotAWaveFile_FailsWithValidationFailedAndWritesNothing()
    {
        var result = await _harness.UploadSound().ExecuteAsync(Caller.Admin, _clip, Name("Mp3"), new byte[] { 0x49, 0x44, 0x33 }, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Empty(_harness.Sounds.Clips);
        Assert.Equal(0, _harness.Sounds.Writes);
    }

    [Fact]
    public async Task UploadSound_FromADevice_IsForbiddenAndWritesNothing()
    {
        var result = await _harness.UploadSound().ExecuteAsync(Build.DeviceCaller, _clip, Name("Applause"), Wave(), CancellationToken.None);

        Build.Fails(DomainErrorCode.Forbidden, result);
        Assert.Empty(_harness.Sounds.Clips);
    }

    // RenameSound

    [Fact]
    public async Task RenameSound_ChangesOnlyTheName_AndPublishes()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Old"), 1000, 100);

        var result = await _harness.RenameSound().ExecuteAsync(Caller.Admin, _clip, Name("  New name  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", _harness.Sounds.Clips[_clip].Name.Value);
        Assert.Equal(_clip, _harness.Sounds.Clips[_clip].Id);
        Assert.Equal([new SoundsChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task RenameSound_UnknownClip_FailsWithSoundNotFound()
    {
        var result = await _harness.RenameSound().ExecuteAsync(Caller.Admin, _clip, Name("New"), CancellationToken.None);

        Build.Fails(DomainErrorCode.SoundNotFound, result);
    }

    [Fact]
    public async Task RenameSound_FromADevice_IsForbidden()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Old"), 1000, 100);

        var result = await _harness.RenameSound().ExecuteAsync(Build.DeviceCaller, _clip, Name("New"), CancellationToken.None);

        Build.Fails(DomainErrorCode.Forbidden, result);
        Assert.Equal("Old", _harness.Sounds.Clips[_clip].Name.Value);
    }

    // DeleteSound

    [Fact]
    public async Task DeleteSound_UnusedClip_RemovesItAndPublishes()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Old"), 1000, 100);

        var result = await _harness.DeleteSound().ExecuteAsync(Caller.Admin, _clip, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_harness.Sounds.Clips);
        Assert.Equal([new SoundsChanged()], _harness.Events.Published);
    }

    [Fact]
    public async Task DeleteSound_ClipUsedByAKey_IsRefusedAndKeepsTheClip()
    {
        _harness.Sounds.Clips[_clip] = new SoundClip(_clip, Name("Applause"), 1000, 100);
        var profile = Build.ProfileWith("Main", new PlaySound(_clip));
        _harness.Profiles.Add(profile);

        var result = await _harness.DeleteSound().ExecuteAsync(Caller.Admin, _clip, CancellationToken.None);

        Build.Fails(DomainErrorCode.ValidationFailed, result);
        Assert.Contains(_clip.Value, _harness.Sounds.Clips.Keys.Select(id => id.Value));
        Assert.Empty(_harness.Events.Published);
    }

    [Fact]
    public async Task DeleteSound_UnknownClip_FailsWithSoundNotFound()
    {
        var result = await _harness.DeleteSound().ExecuteAsync(Caller.Admin, _clip, CancellationToken.None);

        Build.Fails(DomainErrorCode.SoundNotFound, result);
    }
}
