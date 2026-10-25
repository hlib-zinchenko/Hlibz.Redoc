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
/// view. The pages and their small scripts carry an ETag and revalidate, because they change
/// whenever the app's configuration does.
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
    public static Task WritePage(HttpContext context, string html, string? policy)
    {
        if (policy is not null
            && !context.Response.Headers.ContainsKey(HeaderNames.ContentSecurityPolicy))
        {
            context.Response.Headers.ContentSecurityPolicy = policy;
        }

        return Write(context, html, "text/html");
    }

    /// <summary>Serves one of the small generated files: the page, its CSS or its scripts.</summary>
    public static Task Write(HttpContext context, string content, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        return context.Response.Body.WriteAsync(bytes).AsTask();
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

        foreach (string? candidate in context.Request.Headers.IfNoneMatch)
        {
            if (candidate is not null
                && candidate.Contains(content.ETag, StringComparison.Ordinal))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }
        }

        response.ContentType = "text/javascript; charset=utf-8";
        if (content.ContentEncoding is { } encoding)
        {
            response.Headers.ContentEncoding = encoding;
        }

        response.ContentLength = content.Bytes.Length;
        await response.Body.WriteAsync(content.Bytes);
    }
}
