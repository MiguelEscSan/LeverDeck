using LeverDeck.Application.Common;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds;

/// <summary>
/// Command (admin scope): stores an uploaded WAV under a new <see cref="SoundId"/> chosen by the editor. The file is
/// checked as a PCM WAV first; an id is written once, so a clip's file never changes under a key that plays it.
/// </summary>
public sealed class UploadSound(ISoundStore sounds, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, SoundId sound, SoundName name, ReadOnlyMemory<byte> wave, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            var info = WaveFile.Parse(wave.Span);
            if (await sounds.FindAsync(sound, cancellationToken).ConfigureAwait(false) is not null)
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Sound {sound} already exists.");
            }

            var clip = new SoundClip(sound, name, info.DurationMs, wave.Length);
            await sounds.SaveAsync(clip, wave, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SoundsChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
