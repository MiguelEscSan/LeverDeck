using LeverDeck.Application.Common;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Security;
using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds;

/// <summary>Command (admin scope): gives a stored clip another name. Keys keep referring to it by id.</summary>
public sealed class RenameSound(ISoundStore sounds, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, SoundId sound, SoundName name, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            if (await sounds.FindAsync(sound, cancellationToken).ConfigureAwait(false) is null)
            {
                return UseCase.Fail(DomainErrorCode.SoundNotFound, $"Sound {sound} does not exist.");
            }

            await sounds.RenameAsync(sound, name, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SoundsChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
