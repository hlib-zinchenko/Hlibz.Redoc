using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// What <c>MapReDocDocuments</c> actually serves, over a real server.
/// </summary>
public class ReDocPagesTests
{
    [Fact]
    public async Task Page_HasNoInlineScriptOrStyle()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        // The whole reason the config, the picker and the color-mode bootstrap are served as
        // files: a page with anything inline needs 'unsafe-inline' or a nonce in script-src, and
        // then there is little point owning the template at all.
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Regex.Matches(html, "<script(?![^>]*\\bsrc=)", RegexOptions.IgnoreCase));
        Assert.DoesNotContain("onload=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Page_ReferencesItsFilesRelatively()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("href=\"redoc-page.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"redoc-colormode.js\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"module\" src=\"redoc-init.js\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_EncodesTheDocumentTitle()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes", "Taxes <script>alert(1)</script>")]);

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<title>Taxes &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Taxes <script>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReDocColorMode.Dark, "dark")]
    [InlineData(ReDocColorMode.Light, "light")]
    public async Task Page_StampsAKnownColorModeBeforeTheFirstByte(
        ReDocColorMode mode, string expected)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.DefaultColorMode = mode);

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains($"<html lang=\"en\" class=\"{expected}\"", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<meta name=\"color-scheme\" content=\"{expected}\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_WithSystemColorMode_LeavesTheClassToTheScript()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains(
            "<meta name=\"color-scheme\" content=\"light dark\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ColorModeScript_ReadsReDocsOwnStorageKey()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string script = await GetAsync(app, "/api-docs/taxes/redoc-colormode.js");

        // ReDoc 3 keeps the viewer's choice here and stamps the same class; this file does it
        // from a served script because ReDoc's own version of it is inline and a strict CSP
        // blocks it.
        Assert.Contains("localStorage.getItem('redoc.colorMode')", script, StringComparison.Ordinal);
        Assert.Contains("classList.add(mode)", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitScript_CarriesTheSpecUrlAndTheConfig()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string init = await GetAsync(app, "/api-docs/taxes/redoc-init.js");

        Assert.Contains("import { init } from", init, StringComparison.Ordinal);
        Assert.Contains("\"/openapi/taxes.json\"", init, StringComparison.Ordinal);
        // Off unless the app opts in: a documentation page shouldn't call a third party.
        Assert.Contains("\"disableTelemetry\":true", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitScript_ImportsTheSharedBundle_FromEveryRouteDepth()
    {
        await using ReDocTestApp shallow = await ReDocTestApp.StartAsync(
            configure: docs => docs.RoutePrefix = string.Empty);
        await using ReDocTestApp deep = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes", Route: "a/b/c"), new ReDocDocument("fees")]);

        Assert.Contains(
            "\"../_redoc/redoc.standalone.js\"",
            await GetAsync(shallow, "/taxes/redoc-init.js"),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"../../../api-docs/_redoc/redoc.standalone.js\"",
            await GetAsync(deep, "/a/b/c/redoc-init.js"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PickerScript_IsAFileListingEveryDocument()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");

        Assert.DoesNotContain("<script", picker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            """var docs = [{"label":"locations","href":"../../api-docs/locations/","current":false},"""
            + """{"label":"taxes","href":"../../api-docs/taxes/","current":true}];""",
            picker,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SingleDocument_GetsNoPicker()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes")]);

        string html = await GetAsync(app, "/api-docs/taxes/");
        HttpResponseMessage picker = await app.GetAsync("/api-docs/taxes/redoc-picker.js");

        Assert.DoesNotContain("redoc-picker.js", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, picker.StatusCode);
    }

    [Theory]
    [InlineData("/base/api-docs")]
    [InlineData("/base/api-docs/")]
    public async Task RoutePrefix_RedirectsToTheFirstDocument(string path)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(pathBase: "/base");

        HttpResponseMessage response = await app.GetAsync(path);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/base/api-docs/locations/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RoutePrefix_WithTheRedirectOff_IsNotServed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.RedirectToFirstDocument = false);

        HttpResponseMessage response = await app.GetAsync("/api-docs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PageFolderWithoutItsTrailingSlash_Redirects()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await app.GetAsync("/api-docs/taxes");

        // Serving the page here would leave every relative href on it resolving one folder up.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("taxes/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task IndexHtml_ServesTheSamePageAsTheFolder()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        Assert.Equal(
            await GetAsync(app, "/api-docs/taxes/"),
            await GetAsync(app, "/api-docs/taxes/index.html"));
    }

    [Fact]
    public async Task Stylesheet_CarriesTheToolbarAndPickerRules()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        Assert.Contains(".hlibz-toolbar", css, StringComparison.Ordinal);
        Assert.Contains(".hlibz-doc-picker", css, StringComparison.Ordinal);
        // The controls borrow ReDoc 3's own variables rather than carrying a palette.
        Assert.Contains("var(--layer-color", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stylesheet_EndsWithTheCallersOwnCss()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.AdditionalCss = ":root { --color-primary-base: #ff0000; }");

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        Assert.EndsWith(
            ":root { --color-primary-base: #ff0000; }\n", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Favicon_IsDeclaredWhenSet()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.Favicon = "/favicon.svg");

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<link rel=\"icon\" href=\"/favicon.svg\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MapReDocDocuments_ReturnsABuilderCoveringEveryEndpointItAdds()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();

        // This is the point of the endpoint-routing form: .RequireAuthorization() and friends
        // reach every file a documentation page is made of, not just its markup.
        app.MapReDocDocuments(ReDocTestApp.DefaultDocuments).WithMetadata(new Marker());

        IReadOnlyList<Endpoint> endpoints =
            [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];
        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.NotNull(endpoint.Metadata.GetMetadata<Marker>()));
    }

    private sealed class Marker;

    private static Task<string> GetAsync(ReDocTestApp app, string path) =>
        app.GetStringAsync(path);
}
