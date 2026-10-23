using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Tests.Fakes;

internal sealed class InMemorySoundStore : ISoundStore
{
    public Dictionary<SoundId, SoundClip> Clips { get; } = [];

    public Dictionary<SoundId, ReadOnlyMemory<byte>> Files { get; } = [];

    public int Writes { get; private set; }

    public Task<IReadOnlyList<SoundClip>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SoundClip>>([.. Clips.Values]);

    public Task<SoundClip?> FindAsync(SoundId sound, CancellationToken cancellationToken) =>
        Task.FromResult(Clips.TryGetValue(sound, out var clip) ? clip : null);

    public Task SaveAsync(SoundClip clip, ReadOnlyMemory<byte> wave, CancellationToken cancellationToken)
    {
        Writes++;
        Clips[clip.Id] = clip;
        Files[clip.Id] = wave;
        return Task.CompletedTask;
    }

    public Task RenameAsync(SoundId sound, SoundName name, CancellationToken cancellationToken)
    {
        Writes++;
        Clips[sound] = Clips[sound].Renamed(name);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SoundId sound, CancellationToken cancellationToken)
    {
        Writes++;
        Clips.Remove(sound);
        Files.Remove(sound);
        return Task.CompletedTask;
    }
}
