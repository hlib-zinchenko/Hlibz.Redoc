using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The ReDoc bundle this package embeds and serves.
/// </summary>
/// <remarks>
/// It is stored gzipped and sent that way, so the 2.2 MB bundle neither sits in memory
/// uncompressed nor gets compressed per request. <see cref="Raw"/> exists only for the client that
/// doesn't accept gzip, which in practice is no browser.
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

    private const string ResourceName = "redoc.standalone.js.gz";

    private static readonly byte[] GzipBytes = Load();

    /// <summary>The bundle as stored: gzip-compressed, ready to send as-is.</summary>
    public static ReadOnlyMemory<byte> Gzip => GzipBytes;

    /// <summary>
    /// A strong ETag over the stored bytes. The bundle can't change within a process, so this is
    /// computed once and is stable across restarts and across every instance of a deployment.
    /// </summary>
    public static string ETag { get; } =
        $"\"{Convert.ToHexString(SHA256.HashData(GzipBytes))[..32].ToLowerInvariant()}\"";

    /// <summary>The bundle decompressed. Only used to answer a request that won't take gzip.</summary>
    public static byte[] Raw()
    {
        using MemoryStream source = new(GzipBytes, writable: false);
        using GZipStream decompressor = new(source, CompressionMode.Decompress);
        using MemoryStream buffer = new(capacity: GzipBytes.Length * 4);
        decompressor.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] Load()
    {
        using Stream? stream = typeof(ReDocBundle).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The embedded ReDoc bundle '{ResourceName}' is missing from "
                + "Hlibz.Redoc.AspNetCore. This is a packaging bug, not something an app can "
                + "cause.");

        using MemoryStream buffer = new(capacity: (int)stream.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
