using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace LeverDeck.App;

/// <summary>
/// Serves the PWA embedded in the executable (see <c>WebUi.targets</c>): static files from the embedded manifest,
/// and <c>index.html</c> for any other GET that is not an agent route, so client-side routes survive a reload.
/// </summary>
internal static class WebUi
{
    /// <summary>Embedded resource root; the build links every web file under it.</summary>
    public const string ResourceRoot = "wwwroot";

    private const string IndexFile = "index.html";

    /// <summary>Agent routes, present or planned; the SPA fallback never answers under them.</summary>
    private static readonly string[] ReservedPrefixes = ["/api", "/ws", "/admin/ws", "/debug", "/health"];

    public static bool IsReserved(PathString path) =>
        ReservedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    public static WebApplication UseLeverDeckWebUi(this WebApplication app)
    {
        var files = new ManifestEmbeddedFileProvider(typeof(WebUi).Assembly, ResourceRoot);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
            OnPrepareResponse = context =>
                context.Context.Response.Headers.CacheControl = WebUiCachePolicy.For(context.Context.Request.Path),
        });

        // Lowest-priority route for paths without a file extension: every mapped endpoint wins over it.
        app.MapFallback(context => ServeIndexAsync(context, files));
        return app;
    }

    private static async Task ServeIndexAsync(HttpContext context, ManifestEmbeddedFileProvider files)
    {
        var request = context.Request;
        var response = context.Response;
        var index = files.GetFileInfo(IndexFile);

        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) || IsReserved(request.Path) || !index.Exists)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength = index.Length;
        response.Headers.CacheControl = WebUiCachePolicy.NoCache;
        response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

        if (HttpMethods.IsHead(request.Method))
        {
            return;
        }

        await using var stream = index.CreateReadStream();
        await stream.CopyToAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }
}
