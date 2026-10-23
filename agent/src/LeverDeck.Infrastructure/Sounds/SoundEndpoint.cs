using LeverDeck.Application.Security;
using LeverDeck.Application.Sounds;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Sounds;
using LeverDeck.Infrastructure.Persistence;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace LeverDeck.Infrastructure.Sounds;

/// <summary>
/// The editor's sound uploads and previews, over HTTP because a clip is larger than the admin socket accepts. Both need
/// the admin token in the <c>X-LeverDeck-Admin</c> header, so the phone can neither upload nor fetch a clip.
/// <c>PUT /api/sounds/&lt;id&gt;.wav?name=…</c> stores a new PCM WAV (50 MB at most); <c>GET</c> returns a stored one.
/// </summary>
public sealed class SoundEndpoint(FileSoundStore store, AdminTokenStore adminTokens, UploadSound upload)
{
    public const string PathPrefix = "/api/sounds";
    public const string Route = PathPrefix + "/{file}";
    public const string AdminTokenHeader = "X-LeverDeck-Admin";
    public const string NameQuery = "name";

    private const int MaxNameQueryLength = 200;
    private const int CopyBufferBytes = 81_920;

    public async Task PutAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.Response;
        if (!IsAdmin(context))
        {
            response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (SoundIdOf(context) is not { } sound)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var name = context.Request.Query[NameQuery].ToString();
        if (name.Length > MaxNameQueryLength || SoundMapper.ToSoundName(name) is not { IsSuccess: true } parsedName)
        {
            await WriteAsync(response, StatusCodes.Status422UnprocessableEntity, "The sound name must be 1–60 characters.").ConfigureAwait(false);
            return;
        }

        if (context.Request.ContentLength is > WaveFile.MaxBytes)
        {
            await WriteAsync(response, StatusCodes.Status413PayloadTooLarge, "A sound must be at most 50 MB.").ConfigureAwait(false);
            return;
        }

        // Kestrel's default request limit is 30 MB; a clip may be 50 MB, so the limit is set for this request only.
        context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize = WaveFile.MaxBytes;
        var wave = await ReadBoundedAsync(context.Request.Body, WaveFile.MaxBytes, context.RequestAborted).ConfigureAwait(false);
        if (wave is null)
        {
            await WriteAsync(response, StatusCodes.Status413PayloadTooLarge, "A sound must be at most 50 MB.").ConfigureAwait(false);
            return;
        }

        var result = await upload.ExecuteAsync(Caller.Admin, sound, parsedName.Value, wave, context.RequestAborted).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        var status = result.Error!.Code == DomainErrorCode.ValidationFailed ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status500InternalServerError;
        await WriteAsync(response, status, result.Error.Message).ConfigureAwait(false);
    }

    public async Task GetAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.Response;
        if (!IsAdmin(context))
        {
            response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var path = SoundIdOf(context) is { } sound ? store.PathOf(sound) : null;
        if (path is null || !File.Exists(path))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.ContentType = "audio/wav";
        response.Headers.CacheControl = "no-store";
        response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        await response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }

    private bool IsAdmin(HttpContext context) =>
        adminTokens.Verify(context.Request.Headers[AdminTokenHeader].ToString());

    /// <summary>The clip id in <c>/api/sounds/&lt;id&gt;.wav</c>, or <c>null</c> for anything else.</summary>
    private static SoundId? SoundIdOf(HttpContext context)
    {
        var file = context.Request.RouteValues["file"] as string;
        return file is not null && file.EndsWith(FileSoundStore.Extension, StringComparison.Ordinal)
            ? SoundId.TryParse(file[..^FileSoundStore.Extension.Length])
            : null;
    }

    /// <summary>Reads the whole body, or <c>null</c> as soon as it is longer than <paramref name="limit"/>.</summary>
    private static async Task<byte[]?> ReadBoundedAsync(Stream body, int limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferBytes];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Task WriteAsync(HttpResponse response, int status, string message)
    {
        response.StatusCode = status;
        response.ContentType = "text/plain; charset=utf-8";
        return response.WriteAsync(message);
    }
}
