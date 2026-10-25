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

    /// <summary>Serves one of the small generated files: the page, its CSS or its scripts.</summary>
    public static Task Write(HttpContext context, string content, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        return context.Response.Body.WriteAsync(bytes).AsTask();
    }

    /// <summary>Serves the embedded bundle, gzip-encoded unless the client won't take it.</summary>
    public static async Task WriteBundle(HttpContext context)
    {
        HttpResponse response = context.Response;
        response.Headers.ETag = ReDocBundle.ETag;
        response.Headers.CacheControl = ImmutableCacheControl;
        response.Headers.Vary = HeaderNames.AcceptEncoding;

        foreach (string? candidate in context.Request.Headers.IfNoneMatch)
        {
            if (candidate is not null
                && candidate.Contains(ReDocBundle.ETag, StringComparison.Ordinal))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }
        }

        response.ContentType = "text/javascript; charset=utf-8";

        if (AcceptsGzip(context.Request))
        {
            // The stored bytes, untouched: no per-request compression, and no uncompressed copy
            // of a 2.2 MB file sitting in memory.
            response.Headers.ContentEncoding = "gzip";
            response.ContentLength = ReDocBundle.Gzip.Length;
            await response.Body.WriteAsync(ReDocBundle.Gzip);
            return;
        }

        byte[] raw = ReDocBundle.Raw();
        response.ContentLength = raw.Length;
        await response.Body.WriteAsync(raw);
    }

    private static bool AcceptsGzip(HttpRequest request)
    {
        foreach (string? value in request.Headers.AcceptEncoding)
        {
            if (value is not null
                && value.Contains("gzip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
