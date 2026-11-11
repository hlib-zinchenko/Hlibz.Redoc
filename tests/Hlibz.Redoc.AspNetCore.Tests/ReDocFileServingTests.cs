using System.IO.Compression;
using System.Net;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// How everything other than the bundle is served: the pages and their small files revalidate
/// against an ETag and travel compressed, and the spec is compressed per request.
/// </summary>
public class ReDocFileServingTests
{
    private const string PickerPath = "/api-docs/taxes/redoc-picker.js";

    [Fact]
    public async Task SmallFiles_CarryAnETagAndRevalidate()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        foreach (string path in new[]
        {
            "/api-docs/taxes/",
            "/api-docs/taxes/redoc-page.css",
            "/api-docs/taxes/redoc-init.js",
            "/api-docs/taxes/redoc-colormode.js",
            PickerPath,
        })
        {
            HttpResponseMessage response = await app.GetAsync(path);

            // They change with the app's configuration under the same URL, so no lifetime: a
            // viewer asks every time and is usually told 304.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoCache, path);
            Assert.False(response.Headers.CacheControl?.Private, path);
            Assert.NotNull(response.Headers.ETag);
        }
    }

    [Fact]
    public async Task SmallFile_WithAMatchingETag_IsNotSentAgain()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage first = await app.GetAsync(PickerPath);
        HttpResponseMessage again = await app.GetAsync(
            PickerPath, ("If-None-Match", first.Headers.ETag!.ToString()));

        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        Assert.Empty(
            await again.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("br;q=0, gzip", "gzip")]
    public async Task SmallFile_IsCompressedWhenTheClientAcceptsIt(
        string acceptEncoding, string expected)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string plain = await app.GetStringAsync(PickerPath);
        HttpResponseMessage response =
            await app.GetAsync(PickerPath, ("Accept-Encoding", acceptEncoding));

        Assert.Equal(expected, response.Content.Headers.ContentEncoding.ToString());
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.Equal(plain, await Decode(response));
    }

    [Fact]
    public async Task SmallFile_HasADistinctETagPerEncoding()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage brotli = await app.GetAsync(PickerPath, ("Accept-Encoding", "br"));
        HttpResponseMessage plain = await app.GetAsync(PickerPath);

        Assert.NotEqual(brotli.Headers.ETag?.ToString(), plain.Headers.ETag?.ToString());

        // The plain copy's tag doesn't stand for the brotli one.
        HttpResponseMessage again = await app.GetAsync(
            PickerPath,
            ("Accept-Encoding", "br"),
            ("If-None-Match", plain.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task TinyFile_IsNeverCompressed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response =
            await app.GetAsync("/api-docs/taxes/redoc-init.js", ("Accept-Encoding", "br"));

        Assert.True(response.Content.Headers.ContentLength < ReDocFile.CompressionThreshold);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task TryItScript_IsCompressedAndShared()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());

        HttpResponseMessage taxes = await app.GetAsync(
            "/api-docs/taxes/redoc-tryit.js", ("Accept-Encoding", "br"));
        HttpResponseMessage locations = await app.GetAsync(
            "/api-docs/locations/redoc-tryit.js", ("Accept-Encoding", "br"));

        Assert.Equal("br", taxes.Content.Headers.ContentEncoding.ToString());
        Assert.True(taxes.Content.Headers.ContentLength < Encoding.UTF8.GetByteCount(
            ReDocTryIt.Script) / 2);

        // The same file on every page, so the same tag.
        Assert.Equal(taxes.Headers.ETag, locations.Headers.ETag);
    }

    [Fact]
    public void GatedDocumentFiles_StayOutOfSharedCaches()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddOpenApi("locations");
        builder.Services.AddOpenApi("taxes");
        using WebApplication app = builder.Build();

        ReDocSite site = ReDocSite.Build(
            app.Services,
            [new ReDocDocument("locations"), new ReDocDocument("taxes").RequireAuthorization()],
            new ReDocDocumentsOptions(),
            nameof(ReDocDocumentsBuilderExtensions.MapReDocDocuments));

        // Revalidated either way; a gated page's files are also the viewer's alone, since the
        // next person through a shared proxy isn't necessarily someone the policy lets in.
        Assert.Equal("no-cache", site.Pages.Single(page => page.Name == "locations").CacheControl);
        Assert.Equal(
            "private, no-cache", site.Pages.Single(page => page.Name == "taxes").CacheControl);
    }

    [Fact]
    public async Task BundleUrl_ChangesWithTheBundle()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string init = await app.GetStringAsync("/api-docs/taxes/redoc-init.js");
        HttpResponseMessage bundle = await app.GetAsync(
            $"/api-docs/_redoc/redoc.standalone.js?v={ReDocBundle.UrlVersion}",
            ("Accept-Encoding", "br"));

        // Immutable for a year, so an upgrade must be a new URL, not the old one revalidated.
        Assert.Contains(
            $"redoc.standalone.js?v={ReDocBundle.UrlVersion}\"", init, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
        Assert.StartsWith(
            ReDocBundle.UrlVersion, ReDocBundle.Gzip.ETag.Trim('"'), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_PreloadsExactlyWhatTheInitScriptRequests()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await app.GetStringAsync("/api-docs/taxes/");
        string init = await app.GetStringAsync("/api-docs/taxes/redoc-init.js");
        int start = init.IndexOf('"', StringComparison.Ordinal);
        string bundleHref = init[(start + 1)..init.IndexOf('"', start + 1)];

        // A preload is only used when the later request matches it: the same URL, and for the
        // spec the same mode and credentials as ReDoc's plain fetch(url). Anything else is a second
        // request — for a served spec, a second generation.
        Assert.Contains(
            $"<link rel=\"modulepreload\" href=\"{bundleHref}\">", html, StringComparison.Ordinal);
        Assert.Contains(
            "<link rel=\"preload\" href=\"/openapi/taxes.json\" as=\"fetch\" crossorigin>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("init(\n  \"/openapi/taxes.json\"", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitScript_HandsTheSpecToTheConsole_OnlyWhenThereIsOne()
    {
        await using ReDocTestApp plain = await ReDocTestApp.StartAsync();
        await using ReDocTestApp withConsole = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());

        string without = await plain.GetStringAsync("/api-docs/taxes/redoc-init.js");
        string with = await withConsole.GetStringAsync("/api-docs/taxes/redoc-init.js");

        Assert.DoesNotContain(ReDocPage.SpecHandOffKey, without, StringComparison.Ordinal);
        Assert.Contains(
            $"Symbol.for('{ReDocPage.SpecHandOffKey}')", with, StringComparison.Ordinal);
        Assert.Contains(
            $"Symbol.for('{ReDocPage.SpecHandOffKey}')",
            ReDocTryIt.Script,
            StringComparison.Ordinal);

        // ReDoc still gets the URL, which is what keeps its download link and page actions.
        string initCall = with[with.IndexOf("init(", StringComparison.Ordinal)..];
        Assert.Contains("\"/openapi/taxes.json\"", initCall, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Spec_IsCompressedPerRequest_AndStillNotStored()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("locations"), new ReDocDocument("taxes")],
            options => options.ServeSpecs = true,
            configureServices: builder =>
            {
                builder.Services.AddOpenApi("locations");
                builder.Services.AddOpenApi("taxes");
            },
            configureApp: web =>
            {
                // Enough operations to clear the compression threshold.
                for (int i = 0; i < 30; i++)
                {
                    web.MapGet($"/locations/{i}", () => "ok").WithGroupName("locations");
                }
            });

        string plain = await app.GetStringAsync("/api-docs/locations/openapi.json");
        HttpResponseMessage compressed = await app.GetAsync(
            "/api-docs/locations/openapi.json", ("Accept-Encoding", "br"));

        Assert.True(Encoding.UTF8.GetByteCount(plain) > ReDocFile.CompressionThreshold);
        Assert.Equal("br", compressed.Content.Headers.ContentEncoding.ToString());
        Assert.True(compressed.Headers.CacheControl?.NoStore);
        Assert.Contains("Accept-Encoding", compressed.Headers.Vary);
        Assert.Equal(plain, await Decode(compressed));
    }

    private static async Task<string> Decode(HttpResponseMessage response)
    {
        Stream body = await response.Content.ReadAsStreamAsync(
            TestContext.Current.CancellationToken);
        string? encoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
        await using Stream decoded = encoding switch
        {
            "br" => new BrotliStream(body, CompressionMode.Decompress),
            "gzip" => new GZipStream(body, CompressionMode.Decompress),
            _ => body,
        };
        using StreamReader reader = new(decoded, Encoding.UTF8);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
