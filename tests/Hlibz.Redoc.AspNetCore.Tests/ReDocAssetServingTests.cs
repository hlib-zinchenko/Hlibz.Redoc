using System.Net;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// How the bundle is served: the part both existing ReDoc hosts get wrong, which is why it is
/// worth pinning down.
/// </summary>
public class ReDocAssetServingTests
{
    private const string BundlePath = "/api-docs/_redoc/redoc.standalone.js";

    [Fact]
    public async Task Bundle_ForABrowser_IsSentAsTheStoredBrotliBytes()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "gzip, deflate, br");

        // Brotli wherever it's offered: 507 KB against gzip's 634 KB, and every browser that
        // speaks it speaks gzip too, so preferring it costs nothing.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", response.Content.Headers.ContentEncoding.ToString());
        Assert.Equal(ReDocBundle.Brotli.Bytes.Length, response.Content.Headers.ContentLength);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
    }

    [Fact]
    public void Bundle_IsSmallerAsBrotliThanAsGzip()
    {
        // The reason the second copy is in the assembly at all. If an upgrade ever reverses this,
        // the brotli resource is dead weight and this says so.
        Assert.True(ReDocBundle.Brotli.Bytes.Length < ReDocBundle.Gzip.Bytes.Length);
    }

    [Fact]
    public async Task Bundle_ForAClientThatOnlyTakesGzip_IsSentAsTheStoredGzipBytes()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "gzip");

        Assert.Equal("gzip", response.Content.Headers.ContentEncoding.ToString());
        Assert.Equal(ReDocBundle.Gzip.Bytes.Length, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Bundle_HonoursAZeroQualityRatherThanMatchingTheName()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        // "br;q=0" contains "br" and means the opposite of accepting it.
        HttpResponseMessage response = await GetAsync(app, BundlePath, "br;q=0, gzip");

        Assert.Equal("gzip", response.Content.Headers.ContentEncoding.ToString());
    }

    [Fact]
    public async Task Bundle_PrefersTheEncodingTheClientRatesHigher()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "br;q=0.5, gzip;q=1.0");

        Assert.Equal("gzip", response.Content.Headers.ContentEncoding.ToString());
    }

    [Fact]
    public async Task Bundle_IsCacheableForever()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "gzip");

        // The bundle can only change with the package version, so there is nothing to
        // revalidate. Redoc.AspNetCore sends no Cache-Control at all; Swashbuckle sends
        // max-age=0, which costs a round trip on every page view.
        Assert.Equal(
            "public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
        Assert.Equal(ReDocBundle.Gzip.ETag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Bundle_HasADistinctETagPerEncoding()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage brotli = await GetAsync(app, BundlePath, "br");
        HttpResponseMessage gzip = await GetAsync(app, BundlePath, "gzip");

        // Different representations of one resource. A cache that saw them as the same could hand
        // brotli bytes to a client that only reads gzip.
        Assert.NotEqual(brotli.Headers.ETag?.ToString(), gzip.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Bundle_WithAMatchingETag_IsNotSentAgain()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await app.GetAsync(
            BundlePath, ("Accept-Encoding", "gzip"), ("If-None-Match", ReDocBundle.Gzip.ETag));

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bundle_WithAnotherEncodingsETag_IsSentAgain()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        // The cached copy is gzip; this request will be answered with brotli, so it isn't a hit.
        HttpResponseMessage response = await app.GetAsync(
            BundlePath, ("Accept-Encoding", "br"), ("If-None-Match", ReDocBundle.Gzip.ETag));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bundle_ForAClientThatWontTakeAnything_IsDecompressed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "identity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(ReDocBundle.Identity.Bytes.Length, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Bundle_IsImportedFromOneUrlByEveryPage()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        // One cached copy for the whole site: switching documents must not re-download 2 MB.
        string fromTaxes = await app.GetStringAsync("/api-docs/taxes/redoc-init.js");
        string fromLocations = await app.GetStringAsync("/api-docs/locations/redoc-init.js");

        Assert.Equal(ImportedUrl(fromTaxes), ImportedUrl(fromLocations));
        Assert.Contains(
            "_redoc/redoc.standalone.js", ImportedUrl(fromTaxes), StringComparison.Ordinal);
    }

    private static string ImportedUrl(string initScript)
    {
        int start = initScript.IndexOf('"', StringComparison.Ordinal);
        int end = initScript.IndexOf('"', start + 1);
        return initScript[(start + 1)..end];
    }

    private static Task<HttpResponseMessage> GetAsync(
        ReDocTestApp app, string path, string acceptEncoding) =>
        app.GetAsync(path, ("Accept-Encoding", acceptEncoding));
}
