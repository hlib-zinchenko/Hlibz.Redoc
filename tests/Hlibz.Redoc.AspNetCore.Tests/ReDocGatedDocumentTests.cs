using System.Net;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// <c>RequireAuthorization</c> on a <see cref="ReDocDocument"/>: the one call that gates a
/// document's page, serves and gates the document it renders, and takes it out of the other pages'
/// pickers.
/// </summary>
/// <remarks>
/// The shapes a reader chooses between are in <c>ReDocAuthorizationShapesTests</c>, as status
/// codes. This file is the mechanism underneath: which endpoints carry the policy, what the call
/// implies about serving specs, and the four ways it refuses rather than half-working.
/// </remarks>
public class ReDocGatedDocumentTests
{
    private static IEnumerable<ReDocDocument> Documents =>
    [
        new ReDocDocument("locations"),
        new ReDocDocument("taxes").RequireAuthorization("Employees"),
    ];

    [Fact]
    public void Gating_PutsThePolicyOnEveryEndpointOfThatDocumentAndNoOther()
    {
        IReadOnlyDictionary<string, string[]> policies = PoliciesByRoute(Documents);

        // The page, the stylesheet, both scripts and the spec: the document is private, not just
        // the page that renders it.
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/"]));
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/index.html"]));
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/redoc-page.css"]));
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/redoc-init.js"]));
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/redoc-colormode.js"]));
        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/openapi.json"]));

        // And nothing of the public document's, nor the bundle the whole site shares.
        Assert.Empty(policies["/api-docs/locations/"]);
        Assert.Empty(policies["/api-docs/locations/openapi.json"]);
        Assert.Empty(policies["/api-docs/_redoc/redoc.standalone.js"]);
    }

    [Fact]
    public void GatingWithNoPolicyNames_StillRequiresAnAuthenticatedUser()
    {
        IReadOnlyList<Endpoint> endpoints =
            Map([new ReDocDocument("locations").RequireAuthorization()]);

        Endpoint page = endpoints
            .OfType<RouteEndpoint>()
            .First(endpoint => endpoint.RoutePattern.RawText == "/api-docs/locations/");

        // RequireAuthorization(params string[]) given an empty array attaches no IAuthorizeData at
        // all — a gate that lets everyone through. The mapping takes the parameterless overload
        // instead, which is the default policy.
        IAuthorizeData data = Assert.Single(page.Metadata.OfType<IAuthorizeData>());
        Assert.True(string.IsNullOrEmpty(data.Policy));
    }

    [Fact]
    public void GatingTwice_KeepsBothPolicies()
    {
        IReadOnlyDictionary<string, string[]> policies = PoliciesByRoute(
        [
            new ReDocDocument("locations")
                .RequireAuthorization("Employees")
                .RequireAuthorization("Auditors"),
        ]);

        // Both, the way they would add up on an endpoint builder. Rebuilding the document from its
        // base properties alone would have dropped the first call silently.
        Assert.Equal(
            "Employees,Auditors",
            string.Join(',', policies["/api-docs/locations/"]));
    }

    [Fact]
    public async Task Gating_MakesEveryPageInTheCallServeItsOwnSpec()
    {
        await using ReDocTestApp app = await StartAsync(Documents);

        // ServeSpecs is off and MapOpenApi is unmapped, yet the *public* page has a spec: one
        // gated document puts the whole site in "we serve the specs" mode, because a page left
        // reading MapOpenApi's endpoint would need an endpoint that serves the gated document too.
        HttpResponseMessage spec = await app.GetAsync("/api-docs/locations/openapi.json");
        Assert.Equal(HttpStatusCode.OK, spec.StatusCode);

        string init = await app.GetStringAsync("/api-docs/locations/redoc-init.js");
        Assert.Contains("\"openapi.json\"", init, StringComparison.Ordinal);
        Assert.DoesNotContain("/openapi/locations.json", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gating_LeavesTheSharedBundleReadable()
    {
        await using ReDocTestApp app = await StartAsync(Documents);

        // One endpoint serves it to every page, so gating it with a single document would stop
        // every other page rendering — and it is ReDoc's own published JavaScript either way.
        HttpResponseMessage bundle = await app.GetAsync("/api-docs/_redoc/redoc.standalone.js");

        Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
    }

    [Fact]
    public void Gating_ComposesWithForDocument()
    {
        WebApplication app = Build();
        ReDocDocumentsConventionBuilder mapped = app.MapReDocDocuments(Documents);
        mapped.ForDocument("taxes").WithMetadata(new Marker());

        RouteEndpoint page = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .First(endpoint => endpoint.RoutePattern.RawText == "/api-docs/taxes/");

        // Two routes to the same endpoints: the document's own policy, and whatever else the
        // convention builder is used for afterwards.
        Assert.Equal("Employees", Assert.Single(page.Metadata.OfType<IAuthorizeData>()).Policy);
        Assert.NotNull(page.Metadata.GetMetadata<Marker>());
    }

    [Fact]
    public void Gating_ADocumentThatSaysWhereItsSpecIs_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Map(
            [
                new ReDocDocument("locations", SpecUrl: "https://example.test/v1.json")
                    .RequireAuthorization("Employees"),
            ]));

        // A locked page in front of an open document is the one outcome the feature exists to
        // rule out, so it is refused rather than mounted.
        Assert.Contains("requires authorization", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "https://example.test/v1.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gating_WithSpecUrlTemplateSetAsWell_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Map(Documents, options => options.SpecUrlTemplate = "/specs/{documentName}.json"));

        // Same reasoning as ServeSpecs' own conflict: two settings, one answer each, about where
        // a page reads its spec. Silently picking one is how a page ends up reading a spec its
        // policy doesn't cover.
        Assert.Contains("requires authorization", exception.Message, StringComparison.Ordinal);
        Assert.Contains("/specs/{documentName}.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gating_ADocumentNoProviderOffers_ThrowsNamingAuthorizationAsTheReason()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Map(
                [new ReDocDocument("nope").RequireAuthorization()],
                options => options.ValidateDocumentNames = false));

        // The same failure ServeSpecs has, but the message has to name the setting the caller
        // actually wrote, which here is the gating rather than an option they never touched.
        Assert.Contains("requires authorization", exception.Message, StringComparison.Ordinal);
        Assert.Contains("named 'nope'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gating_InTheMiddlewareForm_Throws()
    {
        WebApplication app = Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => app.UseReDocDocuments(Documents));

        // Middleware carries no metadata, so there is nothing for a policy to attach to. Serving
        // the page anyway would be the half-working outcome: a page the caller believes is gated.
        Assert.Contains(
            "UseReDocDocuments cannot enforce", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MapReDocDocuments", exception.Message, StringComparison.Ordinal);
    }

    private sealed class Marker;

    private static Task<ReDocTestApp> StartAsync(IEnumerable<ReDocDocument> documents)
    {
        IReadOnlyList<ReDocDocument> docs = [.. documents];

        return ReDocTestApp.StartAsync(
            docs,
            configureServices: builder =>
            {
                foreach (ReDocDocument document in docs)
                {
                    builder.Services.AddOpenApi(document.Name);
                }

                builder.Services
                    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie();
                builder.Services.AddAuthorizationBuilder().AddPolicy(
                    "Employees", policy => policy.RequireAuthenticatedUser());
            },
            configureApp: web =>
            {
                web.UseAuthentication();
                web.UseAuthorization();
            });
    }

    /// <summary>
    /// An app whose provider offers the two documents this file maps, and nothing else — so a
    /// test can map a name nobody registered and watch it be refused.
    /// </summary>
    private static WebApplication Build()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddOpenApi("locations");
        builder.Services.AddOpenApi("taxes");

        return builder.Build();
    }

    /// <summary>
    /// Maps the documents against a provider that knows them, and returns the endpoints with
    /// their conventions applied — enumerating the data sources is what runs them.
    /// </summary>
    private static IReadOnlyList<Endpoint> Map(
        IEnumerable<ReDocDocument> documents, Action<ReDocDocumentsOptions>? configure = null)
    {
        WebApplication app = Build();
        app.MapReDocDocuments(documents, configure);
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];
    }

    private static IReadOnlyDictionary<string, string[]> PoliciesByRoute(
        IEnumerable<ReDocDocument> documents) =>
        Map(documents)
            .OfType<RouteEndpoint>()
            .ToDictionary(
                endpoint => $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}",
                endpoint =>
                    endpoint.Metadata.OfType<IAuthorizeData>()
                        .Select(data => data.Policy ?? string.Empty)
                        .ToArray(),
                StringComparer.Ordinal);
}
