using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// <c>ServeSpecs</c>: each page serving its own OpenAPI document from its own route, which is what
/// makes <c>ForDocument(...).RequireAuthorization()</c> cover the document and not just the page
/// that renders it.
/// </summary>
/// <remarks>
/// Against the real <c>AddOpenApi</c> provider rather than a stand-in, for the same reason the
/// document-name check is: the type and method names resolved by string are the fragile part, and
/// a fake would be written to match whatever this package already assumes.
/// </remarks>
public class ReDocSpecServingTests
{
    private const string SpecPath = "/api-docs/locations/openapi.json";

    private static readonly IEnumerable<ReDocDocument> Documents =
        [new ReDocDocument("locations"), new ReDocDocument("taxes")];

    [Fact]
    public async Task ServeSpecs_ServesTheDocumentBesideThePage()
    {
        await using ReDocTestApp app = await StartAsync();

        HttpResponseMessage response = await app.GetAsync(SpecPath);
        string json =
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // A real document from the registered provider, not an empty object.
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("openapi", out _));

        // AddOpenApi titles a document "{assembly} | {name}", so the name is what identifies it:
        // this page is serving its own document and not some other registered one.
        Assert.Contains(
            "locations",
            document.RootElement.GetProperty("info").GetProperty("title").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServeSpecs_SendsTheSpecNoStore()
    {
        await using ReDocTestApp app = await StartAsync();

        HttpResponseMessage response = await app.GetAsync(SpecPath);

        // The one response here that may be nobody else's business: a shared cache holding a gated
        // document would hand it to the next caller.
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task ServeSpecs_PointsThePageAtItsOwnSpec()
    {
        await using ReDocTestApp app = await StartAsync();

        string init = await app.GetStringAsync("/api-docs/locations/redoc-init.js");

        // Relative, like every other href on the page, so it survives a path-rewriting proxy.
        Assert.Contains("\"openapi.json\"", init, StringComparison.Ordinal);
        Assert.DoesNotContain("/openapi/locations.json", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutServeSpecs_ThePageReadsTheTemplateUrlAndServesNothing()
    {
        await using ReDocTestApp app = await StartAsync(serveSpecs: false);

        string init = await app.GetStringAsync("/api-docs/locations/redoc-init.js");
        HttpResponseMessage spec = await app.GetAsync(SpecPath);

        Assert.Contains("\"/openapi/locations.json\"", init, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, spec.StatusCode);
    }

    [Fact]
    public async Task ServeSpecs_SitsBesideMapOpenApiRatherThanReplacingIt()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            Documents,
            options => options.ServeSpecs = true,
            configureServices: builder => AddDocuments(builder.Services, Documents),
            configureApp: web => web.MapOpenApi());

        HttpResponseMessage mapped = await app.GetAsync("/openapi/locations.json");
        HttpResponseMessage own = await app.GetAsync(SpecPath);

        // Nothing is taken away: an app that maps MapOpenApi for its own reasons — a client
        // generator, a linter, another UI — keeps that URL, and the page still reads the copy
        // that its own policy covers. Which also means removing MapOpenApi is a separate,
        // deliberate step, and the one that actually closes a gated document.
        Assert.Equal(HttpStatusCode.OK, mapped.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        string init = await app.GetStringAsync("/api-docs/locations/redoc-init.js");
        Assert.Contains("\"openapi.json\"", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServeSpecs_LeavesADocumentThatSaysWhereItsSpecIs()
    {
        await using ReDocTestApp app = await StartAsync(
            documents: [new ReDocDocument("locations", SpecUrl: "https://example.test/v1.json")]);

        string init = await app.GetStringAsync("/api-docs/locations/redoc-init.js");
        HttpResponseMessage spec = await app.GetAsync(SpecPath);

        Assert.Contains("https://example.test/v1.json", init, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, spec.StatusCode);
    }

    [Fact]
    public void ServeSpecs_PutsTheSpecInThatDocumentsOwnGroup()
    {
        // The whole point of the feature: one call, and the document is private rather than just
        // the page that renders it.
        IReadOnlyList<string> marked = Marked(
            builder => builder.ForDocument("locations").WithMetadata(new Marker()));

        Assert.Contains("/api-docs/locations/openapi.json", marked);
        Assert.DoesNotContain("/api-docs/taxes/openapi.json", marked);
    }

    [Fact]
    public void WholeBuilder_CoversEverySpecToo()
    {
        IReadOnlyList<string> marked = Marked(builder => builder.WithMetadata(new Marker()));

        Assert.Contains("/api-docs/locations/openapi.json", marked);
        Assert.Contains("/api-docs/taxes/openapi.json", marked);
    }

    [Fact]
    public void ServeSpecs_ForADocumentNoProviderOffers_ThrowsWhenMapped()
    {
        // Name validation is off, so this is the spec source failing rather than the name check:
        // a page mounted here would answer its own spec request with a 500.
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Map(
                [new ReDocDocument("nope")],
                options =>
                {
                    options.ServeSpecs = true;
                    options.ValidateDocumentNames = false;
                }));

        Assert.Contains("ServeSpecs is on", exception.Message, StringComparison.Ordinal);
        Assert.Contains("named 'nope'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ServeSpecs_WithSpecUrlTemplateSetAsWell_Throws()
    {
        // Two settings, one answer each, about where every page reads its spec from. Silently
        // picking one is how a page ends up reading a spec its policy doesn't cover.
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Map(
                Documents,
                options =>
                {
                    options.ServeSpecs = true;
                    options.SpecUrlTemplate = "/specs/{documentName}.json";
                }));

        Assert.Contains(
            "ServeSpecs and SpecUrlTemplate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("/specs/{documentName}.json", exception.Message, StringComparison.Ordinal);
    }

    private sealed class Marker;

    private static Task<ReDocTestApp> StartAsync(
        IEnumerable<ReDocDocument>? documents = null, bool serveSpecs = true) =>
        ReDocTestApp.StartAsync(
            documents ?? Documents,
            options => options.ServeSpecs = serveSpecs,
            configureServices: builder => AddDocuments(builder.Services, documents ?? Documents));

    /// <summary>
    /// Maps the documents against a provider that knows them, and returns the endpoints — the
    /// mapping itself is what several of these assert on.
    /// </summary>
    private static IReadOnlyList<Endpoint> Map(
        IEnumerable<ReDocDocument> documents, Action<ReDocDocumentsOptions> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        AddDocuments(builder.Services, Documents);

        WebApplication app = builder.Build();
        app.MapReDocDocuments(documents, configure);
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];
    }

    private static IReadOnlyList<string> Marked(
        Action<ReDocDocumentsConventionBuilder> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        AddDocuments(builder.Services, Documents);

        WebApplication app = builder.Build();
        configure(app.MapReDocDocuments(Documents, options => options.ServeSpecs = true));

        return
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<Marker>() is not null)
                .Select(endpoint => $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}"),
        ];
    }

    private static void AddDocuments(IServiceCollection services, IEnumerable<ReDocDocument> docs)
    {
        foreach (ReDocDocument document in docs)
        {
            services.AddOpenApi(document.Name);
        }
    }
}
