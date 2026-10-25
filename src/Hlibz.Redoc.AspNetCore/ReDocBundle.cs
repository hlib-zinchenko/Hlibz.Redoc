using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The ReDoc bundle this package embeds and serves, in the encodings it is stored in.
/// </summary>
/// <remarks>
/// It is stored compressed and sent that way, so the 2.2 MB bundle neither sits in memory
/// uncompressed nor gets compressed per request. Brotli and gzip are both pre-built, because this
/// file is the same bytes for the life of the package version — paying quality 11 once, before the
/// bundle is ever checked in, is free, where a response-compression middleware would pay a far
/// cheaper setting on every cache miss. <see cref="Identity"/> exists only for the client that
/// accepts neither, which in practice is no browser, and is decompressed on first use rather than
/// at startup.
/// </remarks>
internal static class ReDocBundle
{
    /// <summary>
    /// The ReDoc version in <c>assets/redoc.standalone.js.gz</c>. Kept in step with that file by
    /// hand — <c>assets/README.md</c> records how — and asserted by a test.
    /// </summary>
    public const string Version = "3.0.1";

    /// <summary>The file name the bundle is served under, relative to the asset route.</summary>
    public const string FileName = "redoc.standalone.js";

    private const string GzipResourceName = "redoc.standalone.js.gz";
    private const string BrotliResourceName = "redoc.standalone.js.br";

    // Declared before the properties below, which are initialized from them in this same order.
    private static readonly byte[] GzipBytes = Load(GzipResourceName);
    private static readonly byte[] BrotliBytes = Load(BrotliResourceName);

    private static readonly Lazy<ReDocBundleContent> LazyIdentity = new(Decompress);

    /// <summary>The bundle as stored for <c>Content-Encoding: br</c>.</summary>
    public static ReDocBundleContent Brotli { get; } = new(BrotliBytes, "br");

    /// <summary>The bundle as stored for <c>Content-Encoding: gzip</c>.</summary>
    public static ReDocBundleContent Gzip { get; } = new(GzipBytes, "gzip");

    /// <summary>The bundle decompressed, for a request that will take no encoding we have.</summary>
    public static ReDocBundleContent Identity => LazyIdentity.Value;

    /// <summary>
    /// The stored encoding to answer a request with: brotli where it is accepted, then gzip, then
    /// the decompressed bytes.
    /// </summary>
    /// <remarks>
    /// Brotli first because it is 20% smaller here and every browser that speaks it also speaks
    /// gzip, so preferring it costs nothing. The header is parsed rather than searched for a
    /// substring: <c>br;q=0</c> contains "br" while meaning the opposite, and a client that sends
    /// only one of the two must not be handed the other.
    /// </remarks>
    public static ReDocBundleContent Negotiate(HttpRequest request)
    {
        if (!StringWithQualityHeaderValue.TryParseList(
                request.Headers.AcceptEncoding, out IList<StringWithQualityHeaderValue>? accepted)
            || accepted.Count == 0)
        {
            return Identity;
        }

        double brotli = QualityOf(accepted, "br");
        double gzip = QualityOf(accepted, "gzip");

        // A tie goes to brotli, which is the smaller of the two.
        if (brotli > 0 && brotli >= gzip)
        {
            return Brotli;
        }

        return gzip > 0 ? Gzip : Identity;
    }

    /// <summary>
    /// How much the client wants one encoding: its own quality, else the one it gave <c>*</c>,
    /// else zero. Quality 0 means "not acceptable", so it is reported as such.
    /// </summary>
    private static double QualityOf(
        IEnumerable<StringWithQualityHeaderValue> accepted, string encoding)
    {
        double wildcard = 0;
        foreach (StringWithQualityHeaderValue candidate in accepted)
        {
            if (candidate.Value.Equals(encoding, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Quality ?? 1;
            }

            if (candidate.Value.Equals("*", StringComparison.Ordinal))
            {
                wildcard = candidate.Quality ?? 1;
            }
        }

        return wildcard;
    }

    private static ReDocBundleContent Decompress()
    {
        using MemoryStream source = new(GzipBytes, writable: false);
        using GZipStream decompressor = new(source, CompressionMode.Decompress);
        using MemoryStream buffer = new(capacity: GzipBytes.Length * 4);
        decompressor.CopyTo(buffer);
        return new ReDocBundleContent(buffer.ToArray(), ContentEncoding: null);
    }

    private static byte[] Load(string resourceName)
    {
        using Stream stream = typeof(ReDocBundle).GetTypeInfo().Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded ReDoc bundle '{resourceName}' is missing from "
                + "Hlibz.Redoc.AspNetCore. This is a packaging bug, not something an app can "
                + "cause.");

        using MemoryStream buffer = new(capacity: (int)stream.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>One stored encoding of the bundle, and the ETag that identifies it.</summary>
/// <param name="Bytes">The bytes to send, as stored.</param>
/// <param name="ContentEncoding">
/// The <c>Content-Encoding</c> to declare, or <see langword="null"/> for the decompressed bytes.
/// </param>
internal sealed record ReDocBundleContent(ReadOnlyMemory<byte> Bytes, string? ContentEncoding)
{
    /// <summary>
    /// A strong ETag over these bytes. The bundle can't change within a process, so this is
    /// computed once and is stable across restarts and across every instance of a deployment.
    /// </summary>
    /// <remarks>
    /// One per encoding, and it falls out of hashing what is actually sent: these are different
    /// representations of the same resource, and a shared cache that saw them as one could hand
    /// brotli bytes to a client that only reads gzip. <c>Vary: Accept-Encoding</c> says the same
    /// thing, and together they are what a correct cache needs.
    /// </remarks>
    public string ETag { get; } =
        $"\"{Convert.ToHexString(SHA256.HashData(Bytes.Span))[..32].ToLowerInvariant()}\"";
}
