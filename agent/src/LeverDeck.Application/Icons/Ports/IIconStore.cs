using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Application.Icons.Ports;

/// <summary>Where uploaded icons live. Ids are always <see cref="CustomIcon"/> ids.</summary>
public interface IIconStore
{
    Task<IReadOnlyList<IconId>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(IconId icon, CancellationToken cancellationToken);

    Task SaveAsync(IconId icon, IconImage image, CancellationToken cancellationToken);

    Task DeleteAsync(IconId icon, CancellationToken cancellationToken);
}
