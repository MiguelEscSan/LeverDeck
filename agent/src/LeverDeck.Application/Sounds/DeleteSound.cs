using LeverDeck.Application.Common;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds;

/// <summary>Command (admin scope): deletes a stored clip. A clip still played by any key is refused, as icons are.</summary>
public sealed class DeleteSound(ISoundStore sounds, IProfileRepository profiles, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, SoundId sound, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            if (await sounds.FindAsync(sound, cancellationToken).ConfigureAwait(false) is null)
            {
                return UseCase.Fail(DomainErrorCode.SoundNotFound, $"Sound {sound} does not exist.");
            }

            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            var inUse = stored
                .SelectMany(profile => profile.Pages)
                .SelectMany(page => page.Buttons)
                .Any(button => button.Action is PlaySound play && play.Sound == sound);
            if (inUse)
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Sound {sound} is still played by a key.");
            }

            await sounds.DeleteAsync(sound, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new SoundsChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
}
