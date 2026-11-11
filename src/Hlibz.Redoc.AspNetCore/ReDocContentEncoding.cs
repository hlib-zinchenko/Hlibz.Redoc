using System.IO.Compression;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Hlibz.Redoc.Extensions;

/// <summary>The encodings this package sends a response in.</summary>
internal enum ReDocContentCoding
{
    Identity,
    Gzip,
    Brotli,
}

/// <summary>
/// Choosing a response's <c>Content-Encoding</c>, and producing it, for everything this package
/// serves: the stored bundle, the small generated files and the spec.
/// </summary>
internal static class ReDocContentEncoding
{
    /// <summary>
    /// The encoding to answer a request with: brotli where it is accepted, then gzip, then none.
    /// </summary>
    /// <remarks>
    /// Brotli first because it is the smaller of the two and every browser that speaks it also
    /// speaks gzip, so preferring it costs nothing. The header is parsed rather than searched for
    /// a substring: <c>br;q=0</c> contains "br" while meaning the opposite, and a client that sends
    /// only one of the two must not be handed the other.
    /// </remarks>
    public static ReDocContentCoding Negotiate(HttpRequest request)
    {
        if (!StringWithQualityHeaderValue.TryParseList(
                request.Headers.AcceptEncoding, out IList<StringWithQualityHeaderValue>? accepted)
            || accepted.Count == 0)
        {
            return ReDocContentCoding.Identity;
        }

        double brotli = QualityOf(accepted, "br");
        double gzip = QualityOf(accepted, "gzip");

        // A tie goes to brotli, which is the smaller of the two.
        if (brotli > 0 && brotli >= gzip)
        {
            return ReDocContentCoding.Brotli;
        }

        return gzip > 0 ? ReDocContentCoding.Gzip : ReDocContentCoding.Identity;
    }

    /// <summary>The <c>Content-Encoding</c> token for a coding, or null for none.</summary>
    public static string? HeaderValue(ReDocContentCoding coding) => coding switch
    {
        ReDocContentCoding.Brotli => "br",
        ReDocContentCoding.Gzip => "gzip",
        _ => null,
    };

    /// <summary>
    /// Compresses <paramref name="bytes"/>, or returns <see langword="null"/> when the result
    /// would be no smaller — which is what a client should then get instead.
    /// </summary>
    /// <param name="bytes">The identity bytes.</param>
    /// <param name="coding">Brotli or gzip.</param>
    /// <param name="smallest">
    /// Spend whatever it takes, for something compressed once and sent many times; otherwise be
    /// fast, for something compressed per request.
    /// </param>
    public static byte[]? Compress(byte[] bytes, ReDocContentCoding coding, bool smallest)
    {
        using MemoryStream buffer = new(capacity: Math.Max(256, bytes.Length / 3));
        CompressionLevel level =
            smallest ? CompressionLevel.SmallestSize : CompressionLevel.Fastest;
        using (Stream compressor = coding switch
        {
            ReDocContentCoding.Brotli => new BrotliStream(buffer, level, leaveOpen: true),
            ReDocContentCoding.Gzip => new GZipStream(buffer, level, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(coding), coding, null),
        })
        {
            compressor.Write(bytes);
        }

        return buffer.Length < bytes.Length ? buffer.ToArray() : null;
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
}
