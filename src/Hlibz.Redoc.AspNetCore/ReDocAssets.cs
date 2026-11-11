using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Writes the files this package serves, with the cache headers each deserves.
/// </summary>
/// <remarks>
/// The bundle never changes for a given version of this package, so it is sent <c>immutable</c>
/// with a year's lifetime and a strong ETag — the existing ReDoc hosts send it either with no
/// <c>Cache-Control</c> at all or with <c>max-age=0</c>, which costs a revalidation on every page
/// view. Its URL carries the bundle's hash, so an upgrade is a new URL rather than a year of the
/// old one. The pages and their small scripts are <see cref="ReDocFile"/>s: an ETag and
/// <c>no-cache</c>, because they change whenever the app's configuration does, under the same
/// URL.
/// </remarks>
internal static class ReDocAssets
{
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    /// <summary>
    /// Serves a page, under the configured <c>Content-Security-Policy</c> if there is one.
    /// </summary>
    /// <remarks>
    /// Only the page gets the header: a policy on the stylesheet or the scripts it loads changes
    /// nothing, since the page's own policy already decides what the page may load. An existing
    /// header is left as it is — one the app set covers more than these pages and is the one that
    /// should hold.
    /// </remarks>
    public static Task WritePage(
        HttpContext context, ReDocFile html, string cacheControl, string? policy)
    {
        if (policy is not null
            && !context.Response.Headers.ContainsKey(HeaderNames.ContentSecurityPolicy))
        {
            context.Response.Headers.ContentSecurityPolicy = policy;
        }

        return html.WriteAsync(context, cacheControl);
    }

    /// <summary>
    /// Serves a page's own OpenAPI document, generated for this request.
    /// </summary>
    /// <remarks>
    /// <c>no-store</c>, unlike everything else here: this is the one response whose point is often
    /// that not everyone may have it, and a shared cache holding a gated document would hand it to
    /// the next caller. It also can't carry an ETag — the document is generated per request, as
    /// <c>MapOpenApi</c>'s is, so there is nothing stable to tag it with. It is compressed per
    /// request, at the fast setting, since a spec is usually the largest thing a page fetches
    /// after the bundle and JSON shrinks by around nine-tenths.
    /// </remarks>
    public static async Task WriteSpec(HttpContext context, OpenApiSpecSource spec)
    {
        string json = await spec.GenerateAsync(context.RequestServices);
        byte[] body = Encoding.UTF8.GetBytes(json);

        HttpResponse response = context.Response;
        response.Headers.CacheControl = "no-store";
        response.ContentType = "application/json; charset=utf-8";

        if (body.Length >= ReDocFile.CompressionThreshold)
        {
            response.Headers.Vary = HeaderNames.AcceptEncoding;
            ReDocContentCoding coding = ReDocContentEncoding.Negotiate(context.Request);
            if (coding != ReDocContentCoding.Identity
                && ReDocContentEncoding.Compress(body, coding, smallest: false) is { } compressed)
            {
                response.Headers.ContentEncoding = ReDocContentEncoding.HeaderValue(coding);
                body = compressed;
            }
        }

        response.ContentLength = body.Length;
        await response.Body.WriteAsync(body);
    }

    /// <summary>
    /// Serves the embedded bundle in the best encoding the request accepts: the stored bytes,
    /// untouched, so there is no per-request compression and no uncompressed copy of a 2.2 MB file
    /// sitting in memory.
    /// </summary>
    public static async Task WriteBundle(HttpContext context)
    {
        HttpResponse response = context.Response;
        ReDocBundleContent content = ReDocBundle.Negotiate(context.Request);

        response.Headers.ETag = content.ETag;
        response.Headers.CacheControl = ImmutableCacheControl;
        response.Headers.Vary = HeaderNames.AcceptEncoding;

        if (HoldsCurrent(context.Request, content.ETag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        response.ContentType = "text/javascript; charset=utf-8";
        if (content.ContentEncoding is { } encoding)
        {
            response.Headers.ContentEncoding = encoding;
        }

        response.ContentLength = content.Bytes.Length;
        await response.Body.WriteAsync(content.Bytes);
    }

    /// <summary>
    /// Whether the request's <c>If-None-Match</c> names <paramref name="etag"/>, so a
    /// <c>304</c> will do.
    /// </summary>
    internal static bool HoldsCurrent(HttpRequest request, string etag)
    {
        foreach (string? candidate in request.Headers.IfNoneMatch)
        {
            if (candidate is not null && candidate.Contains(etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
