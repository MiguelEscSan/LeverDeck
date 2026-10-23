using LeverDeck.Application.Common;
using LeverDeck.Application.Security;
using LeverDeck.Application.Sounds.Ports;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Sounds;

namespace LeverDeck.Application.Sounds;

/// <summary>Query (device scope, so phones can list the clips too): the uploaded sound clips, by name.</summary>
public sealed class GetSounds(ISoundStore sounds)
{
    public Task<Result<IReadOnlyList<SoundClip>>> ExecuteAsync(Caller caller, CancellationToken cancellationToken) =>
        UseCase.RunAsync(caller, CallerScope.Device, async () =>
        {
            var stored = await sounds.ListAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<SoundClip> sorted =
            [
                .. stored.OrderBy(clip => clip.Name.Value, StringComparer.OrdinalIgnoreCase).ThenBy(clip => clip.Id.Value, StringComparer.Ordinal),
            ];
            return Result.Success(sorted);
        });
}
