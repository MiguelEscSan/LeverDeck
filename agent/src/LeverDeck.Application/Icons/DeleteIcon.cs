using LeverDeck.Application.Common;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Icons.Ports;
using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Icons;

/// <summary>Command (admin scope): deletes an uploaded icon. An icon still shown by any key is rejected.</summary>
public sealed class DeleteIcon(IIconStore icons, IProfileRepository profiles, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, IconId icon, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            if (!await icons.ExistsAsync(icon, cancellationToken).ConfigureAwait(false))
            {
                return UseCase.Fail(DomainErrorCode.IconNotFound, $"Icon {icon} does not exist.");
            }

            var stored = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            var inUse = stored
                .SelectMany(profile => profile.Pages)
                .SelectMany(page => page.Buttons)
                .Any(button => button.Appearance.Icon == icon);
            if (inUse)
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Icon {icon} is still used by a key.");
            }

            await icons.DeleteAsync(icon, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new IconsChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
