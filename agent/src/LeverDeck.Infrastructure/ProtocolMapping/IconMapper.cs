using LeverDeck.Domain.Common;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Reads the icon fields of <c>saveIcon</c> and <c>deleteIcon</c>.</summary>
public static class IconMapper
{
    public static Result<IconId> ToIconId(string? value) => MappingBoundary.Run(() => new IconId(value ?? string.Empty));

    /// <summary>Decodes the base64 PNG of an upload and checks it as an <see cref="IconImage"/>.</summary>
    public static Result<IconImage> ToImage(string? base64)
    {
        var bytes = new byte[((base64?.Length ?? 0) * 3 / 4) + 3];
        if (base64 is null || !Convert.TryFromBase64String(base64, bytes, out var written))
        {
            return Result.Failure<IconImage>(new DomainError(DomainErrorCode.ValidationFailed, "Icon data must be base64."));
        }

        return MappingBoundary.Run(() => new IconImage(bytes.AsSpan(0, written)));
    }
}
