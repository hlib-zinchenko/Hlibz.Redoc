using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// Which endpoints a convention reaches — the whole site, or one document. Asserted on metadata
/// rather than on a 401, because the question is what <c>MapReDocDocuments</c> hands a policy, not
/// whether ASP.NET's authorization works.
/// </summary>
public class ReDocAuthorizationTests
{
    private static readonly IEnumerable<ReDocDocument> Documents =
        [new ReDocDocument("public"), new ReDocDocument("internal")];

    [Fact]
    public void WholeBuilder_CoversEveryEndpoint_BundleIncluded()
    {
        IReadOnlyList<Endpoint> endpoints = Map(builder => builder.WithMetadata(new Marker()));

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<Marker>()));
    }

    [Fact]
    public void ForDocument_CoversThatDocumentsEndpointsOnly()
    {
        IReadOnlyList<Endpoint> endpoints =
            Map(builder => builder.ForDocument("internal").WithMetadata(new Marker()));

        // Every file the internal page is made of, not just its markup: a stylesheet or a script
        // left unprotected would be a hole, and the init script carries the spec URL.
        Assert.Equal(
            [
                "/api-docs/internal/",
                "/api-docs/internal/index.html",
                "/api-docs/internal/redoc-page.css",
                "/api-docs/internal/redoc-init.js",
                "/api-docs/internal/redoc-colormode.js",
                "/api-docs/internal/redoc-picker.js",
            ],
            Marked(endpoints));
    }

    [Fact]
    public void ForDocument_LeavesTheSharedBundleAlone()
    {
        IReadOnlyList<Endpoint> endpoints =
            Map(builder => builder.ForDocument("internal").WithMetadata(new Marker()));

        // Gating the bundle with one document would stop every other page rendering — it is one
        // endpoint for the whole site, and it is ReDoc's own published JavaScript.
        Endpoint bundle = Single(endpoints, "/api-docs/_redoc/redoc.standalone.js");
        Assert.Null(bundle.Metadata.GetMetadata<Marker>());
    }

    [Fact]
    public void ForDocument_IsCaseInsensitive()
    {
        IReadOnlyList<Endpoint> endpoints =
            Map(builder => builder.ForDocument("INTERNAL").WithMetadata(new Marker()));

        Assert.Contains("/api-docs/internal/", Marked(endpoints));
    }

    [Fact]
    public void ForDocument_WithAnUnknownName_Throws()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();
        ReDocDocumentsConventionBuilder builder = app.MapReDocDocuments(Documents);

        // Silently protecting nothing is the failure mode worth ruling out here.
        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => builder.ForDocument("internl"));

        Assert.Equal("documentName", exception.ParamName);
        Assert.Contains(
            "mapped no document named 'internl'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'internal', 'public'", exception.Message, StringComparison.Ordinal);
    }

    private sealed class Marker;

    private static IReadOnlyList<Endpoint> Map(Action<ReDocDocumentsConventionBuilder> configure)
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();
        configure(app.MapReDocDocuments(Documents));
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];
    }

    private static IReadOnlyList<string> Marked(IReadOnlyList<Endpoint> endpoints) =>
    [
        .. endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<Marker>() is not null)
            .Select(endpoint => $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}"),
    ];

    private static Endpoint Single(IReadOnlyList<Endpoint> endpoints, string pattern) =>
        endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                string.Equals(
                    $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}",
                    pattern,
                    StringComparison.Ordinal));
}
