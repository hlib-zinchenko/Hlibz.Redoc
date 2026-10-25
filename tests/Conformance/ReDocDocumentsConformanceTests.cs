using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hlibz.Redoc.Extensions.Conformance;

/// <summary>
/// <c>UseReDocDocuments</c> end to end: what it rejects, the prefix redirect, and the picker as a
/// host actually serves it, requested through a real pipeline at <c>/{route}/index.html</c>.
/// </summary>
public class ReDocDocumentsConformanceTests
{
    [Fact]
    public void UseReDocDocuments_WithNoDocuments_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments([]));
    }

    [Fact]
    public void UseReDocDocuments_WithDuplicateNames_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("taxes"), new ReDocDocument("Taxes")]));
    }

    [Fact]
    public void UseReDocDocuments_WithSlashInName_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("v1/taxes")]));
    }

    [Fact]
    public void UseReDocDocuments_WithDuplicateRoutes_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("taxes"), new ReDocDocument("fees", Route: "/api-docs/taxes/")]));
    }

    [Fact]
    public void UseReDocDocuments_WithEmptyLinkUrl_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("taxes")],
            docs => docs.Links.Add(new ReDocLink("Swagger UI", " "))));
    }

    [Fact]
    public void UseReDocDocuments_WhenConfigureReDocChangesRoutePrefix_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<InvalidOperationException>(() => app.UseReDocDocuments(
            [new ReDocDocument("taxes")],
            docs => docs.ConfigureReDoc = (redoc, _) => redoc.RoutePrefix = "elsewhere"));
    }

    [Theory]
    [InlineData("/api-docs")]
    [InlineData("/api-docs/")]
    [InlineData("/API-DOCS")]
    public async Task UseReDocDocuments_RoutePrefix_RedirectsToFirstDocument(string path)
    {
        HttpContext context = await SendAsync(
            app => app.UseReDocDocuments([new ReDocDocument("locations"), new ReDocDocument("taxes")]),
            path,
            pathBase: "/base");

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/base/api-docs/locations/", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task UseReDocDocuments_WithCustomFirstRoute_RedirectsToIt()
    {
        HttpContext context = await SendAsync(
            app => app.UseReDocDocuments([new ReDocDocument("taxes", Route: "reference/taxes")]),
            "/api-docs");

        Assert.Equal("/reference/taxes/", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task UseReDocDocuments_WithRedirectDisabled_PassesRoutePrefixThrough()
    {
        HttpContext context = await SendAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes")],
                docs => docs.RedirectToFirstDocument = false),
            "/api-docs");

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task UseReDocDocuments_WhenDocumentIsServedAtRoutePrefix_LetsReDocHandleIt()
    {
        HttpContext context = await SendAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes", Route: "api-docs"), new ReDocDocument("fees")]),
            "/api-docs/");

        // ReDoc's own redirect to its index page, not ours to the first document.
        Assert.Equal(StatusCodes.Status301MovedPermanently, context.Response.StatusCode);
        Assert.Equal("index.html", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Picker_ListsDocumentsWithLinksRelativeToThePage()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("locations", "Locations"), new ReDocDocument("taxes", "Taxes")]),
            "/api-docs/taxes/index.html");

        Assert.Contains(
            """var docs = [{"label":"Locations","href":"../../api-docs/locations/","current":false},""" +
            """{"label":"Taxes","href":"../../api-docs/taxes/","current":true}];""",
            html);
    }

    [Fact]
    public async Task Picker_WithCustomRoutes_LinksAcrossDifferentDepths()
    {
        Action<WebApplication> configure = app => app.UseReDocDocuments(
            [new ReDocDocument("locations"), new ReDocDocument("taxes", Route: "/reference/v2/taxes/")]);

        string nested = await GetPageAsync(configure, "/reference/v2/taxes/index.html");
        string standard = await GetPageAsync(configure, "/api-docs/locations/index.html");

        Assert.Contains("""href":"../../../api-docs/locations/","current":false""", nested);
        Assert.Contains("""href":"../../reference/v2/taxes/","current":false""", standard);
    }

    [Fact]
    public async Task Picker_WithEmptyRoutePrefix_ServesPagesAtTheRoot()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("locations"), new ReDocDocument("taxes")],
                docs => docs.RoutePrefix = ""),
            "/taxes/index.html");

        Assert.Contains("""href":"../locations/","current":false""", html);
    }

    [Fact]
    public async Task Picker_ResolvesAppRelativeLinksAndKeepsOthersAsIs()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes")],
                docs =>
                {
                    docs.Links.Add(new ReDocLink("Swagger UI", "~/swagger"));
                    docs.Links.Add(new ReDocLink("Status", "https://status.example.com/"));
                }),
            "/api-docs/taxes/index.html");

        Assert.Contains(
            """var links = [{"label":"Swagger UI","href":"../../swagger","current":false},""" +
            """{"label":"Status","href":"https://status.example.com/","current":false}];""",
            html);
    }

    [Fact]
    public async Task Picker_EscapesLabelsInsideTheScript()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes", DisplayName: "</script><script>alert('x')</script>")]),
            "/api-docs/taxes/index.html");

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("\\u003C/script\\u003E\\u003Cscript\\u003Ealert(\\u0027x\\u0027)", html);
    }

    [Fact]
    public async Task Picker_EscapesRouteSegmentsInHrefs()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments([new ReDocDocument("taxes & fees")]),
            "/api-docs/taxes & fees/index.html");

        Assert.Contains("""href":"../../api-docs/taxes%20%26%20fees/","current":true""", html);
    }

    [Fact]
    public async Task Picker_AppendsToHeadContentFromConfigureReDoc()
    {
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes")],
                docs => docs.ConfigureReDoc = (redoc, _) => redoc.UseDarkTheme()),
            "/api-docs/taxes/index.html");

        Assert.Contains("--hlibz-control-accent-fg: #09090b", html);
        Assert.Contains("hlibz-doc-picker", html);
    }

    [Fact]
    public async Task ServedPage_CarriesTheTheme_SelectorAndPickerTogether()
    {
        // The theme, the selector and the picker each reach the page through HeadContent, and the
        // picker gets there by appending to whatever UseDarkTheme already wrote. Asserting all
        // three on one served page is what catches a host whose template drops or truncates it.
        string html = await GetPageAsync(
            app => app.UseReDocDocuments(
                [new ReDocDocument("taxes")],
                docs => docs.ConfigureReDoc = (redoc, _) =>
                    redoc.UseDarkTheme(theme => theme.EnableThemeSelector())),
            "/api-docs/taxes/index.html");

        Assert.Contains("--hlibz-control-accent-fg: #09090b", html);
        Assert.Contains("""html[data-redoc-theme="dark"] #redoc-container h5""", html);
        Assert.Contains("hlibz-theme-selector", html);
        Assert.Contains("hlibz-doc-picker", html);
    }

    private static ApplicationBuilder CreateApp()
    {
        return new ApplicationBuilder(new ServiceCollection().AddOptions().BuildServiceProvider());
    }

    private static async Task<string> GetPageAsync(Action<WebApplication> configure, string path)
    {
        HttpContext context = await SendAsync(configure, path);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task<HttpContext> SendAsync(
        Action<WebApplication> configure, string path, string pathBase = "")
    {
        await using WebApplication app = WebApplication.CreateSlimBuilder().Build();
        configure(app);
        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();

        DefaultHttpContext context = new() { RequestServices = app.Services };
        context.Request.Method = HttpMethods.Get;
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await pipeline(context);
        return context;
    }
}
