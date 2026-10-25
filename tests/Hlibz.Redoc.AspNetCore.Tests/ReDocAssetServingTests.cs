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
    public async Task Bundle_IsSentAsTheStoredGzipBytes()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "gzip, deflate, br");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("gzip", response.Content.Headers.ContentEncoding.ToString());
        Assert.Equal(ReDocBundle.Gzip.Length, response.Content.Headers.ContentLength);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
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
        Assert.Equal(ReDocBundle.ETag, response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Bundle_WithAMatchingETag_IsNotSentAgain()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await app.GetAsync(
            BundlePath, ("Accept-Encoding", "gzip"), ("If-None-Match", ReDocBundle.ETag));

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bundle_ForAClientThatWontTakeGzip_IsDecompressed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await GetAsync(app, BundlePath, "identity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(ReDocBundle.Raw().Length, response.Content.Headers.ContentLength);
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
