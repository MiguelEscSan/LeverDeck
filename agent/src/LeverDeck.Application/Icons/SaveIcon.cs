using LeverDeck.Application.Common;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Icons.Ports;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Icons;

/// <summary>
/// Command (admin scope): stores an uploaded icon under a new <see cref="CustomIcon"/> id chosen by the editor. An id
/// is written once: its image never changes, so devices may cache it forever.
/// </summary>
public sealed class SaveIcon(IIconStore icons, IEventPublisher events)
{
    public Task<Result> ExecuteAsync(Caller caller, IconId icon, IconImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(image);
        return UseCase.RunAsync(caller, CallerScope.Admin, async () =>
        {
            if (!CustomIcon.IsCustom(icon))
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"'{icon}' is not an uploaded icon id.");
            }

            if (await icons.ExistsAsync(icon, cancellationToken).ConfigureAwait(false))
            {
                return UseCase.Fail(DomainErrorCode.ValidationFailed, $"Icon {icon} already exists.");
            }

            await icons.SaveAsync(icon, image, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new IconsChanged(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
