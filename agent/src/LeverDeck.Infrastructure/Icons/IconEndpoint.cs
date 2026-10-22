using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace LeverDeck.Infrastructure.Icons;

/// <summary>
/// <c>GET /api/icons/&lt;id&gt;.png</c>: an uploaded icon, for the phone and the editor (an <c>&lt;img&gt;</c> cannot
/// send a token, and an icon is not a secret). An id's image never changes, so it is cached forever; anything that is
/// not an existing uploaded icon is 404.
/// </summary>
public sealed class IconEndpoint(FileIconStore store)
{
    public const string PathPrefix = "/api/icons";
    public const string Route = PathPrefix + "/{file}";

    public async Task HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.Response;
        var file = context.Request.RouteValues["file"] as string;
        var icon = file is not null && file.EndsWith(FileIconStore.Extension, StringComparison.Ordinal)
            ? FileIconStore.TryParse(file[..^FileIconStore.Extension.Length])
            : null;
        var path = icon is null ? null : store.PathOf(icon);
        if (path is null || !File.Exists(path))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.ContentType = "image/png";
        response.Headers.CacheControl = "public, max-age=31536000, immutable";
        response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        response.Headers.ContentSecurityPolicy = "default-src 'none'";
        await response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }
}
