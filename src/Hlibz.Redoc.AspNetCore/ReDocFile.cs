using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// One of the small files a site serves — a page, its stylesheet, a script — encoded once and
/// tagged once, so a request costs a header comparison or a copy of bytes already made.
/// </summary>
/// <remarks>
/// <para>
/// The content is fixed when the site is built, so its ETag is too: a viewer's revalidation is a
/// <c>304</c> with no body. These files are sent <c>no-cache</c> rather than with a lifetime,
/// because unlike the bundle their URLs don't change when they do — a configuration change
/// redeploys them under the same name.
/// </para>
/// <para>
/// Compression happens on the first request that wants each encoding, at the smallest setting,
/// rather than at startup: a site is built once per <c>MapReDocDocuments</c> call, and most of
/// its files are never asked for in most encodings. Files too small to gain anything are never
/// compressed.
/// </para>
/// </remarks>
internal sealed class ReDocFile
{
    /// <summary>Below this, a compressed copy saves less than its headers cost.</summary>
    internal const int CompressionThreshold = 1024;

    private readonly byte[] identity;
    private readonly Lazy<byte[]?> brotli;
    private readonly Lazy<byte[]?> gzip;
    private readonly string hash;

    public ReDocFile(string content, string contentType)
    {
        identity = Encoding.UTF8.GetBytes(content);
        ContentType = $"{contentType}; charset=utf-8";
        hash = Convert.ToHexString(SHA256.HashData(identity))[..32].ToLowerInvariant();
        Compressible = identity.Length >= CompressionThreshold;
        brotli = new(() => ReDocContentEncoding.Compress(
            identity, ReDocContentCoding.Brotli, smallest: true));
        gzip = new(() => ReDocContentEncoding.Compress(
            identity, ReDocContentCoding.Gzip, smallest: true));
    }

    public string ContentType { get; }

    /// <summary>Whether this file is ever sent compressed.</summary>
    public bool Compressible { get; }

    /// <summary>
    /// Writes the file in the best encoding the request accepts, or a <c>304</c> when the
    /// request already holds that representation.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="cacheControl">
    /// <c>no-cache</c>, or <c>private, no-cache</c> for a file of a gated document, which a shared
    /// cache has no business keeping at all.
    /// </param>
    public Task WriteAsync(HttpContext context, string cacheControl)
    {
        ReDocContentCoding coding = Compressible
            ? ReDocContentEncoding.Negotiate(context.Request)
            : ReDocContentCoding.Identity;

        byte[]? body = coding switch
        {
            ReDocContentCoding.Brotli => brotli.Value,
            ReDocContentCoding.Gzip => gzip.Value,
            _ => identity,
        };

        if (body is null)
        {
            coding = ReDocContentCoding.Identity;
            body = identity;
        }

        HttpResponse response = context.Response;

        // One tag per representation, for the same reason the bundle has one per encoding: a
        // cache that took brotli and gzip bytes for the same thing could hand one to a client
        // that only reads the other.
        string etag = coding switch
        {
            ReDocContentCoding.Brotli => $"\"{hash}-br\"",
            ReDocContentCoding.Gzip => $"\"{hash}-gz\"",
            _ => $"\"{hash}\"",
        };

        response.Headers.ETag = etag;
        response.Headers.CacheControl = cacheControl;
        if (Compressible)
        {
            response.Headers.Vary = HeaderNames.AcceptEncoding;
        }

        if (ReDocAssets.HoldsCurrent(context.Request, etag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return Task.CompletedTask;
        }

        response.ContentType = ContentType;
        if (ReDocContentEncoding.HeaderValue(coding) is { } encoding)
        {
            response.Headers.ContentEncoding = encoding;
        }

        response.ContentLength = body.Length;
        return response.Body.WriteAsync(body).AsTask();
    }
}
