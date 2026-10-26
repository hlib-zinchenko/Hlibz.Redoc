using System.Net;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// The three ways a documentation site is set up — everything public, everything internal, and
/// one public document beside a private one — each asserted as the set of answers an anonymous
/// caller gets.
/// </summary>
/// <remarks>
/// <para>
/// These are the README's three examples, which is the point of them: a reader choosing a shape is
/// choosing on the strength of what the snippet does, and a snippet that drifts from the package
/// would be worse than none. The assertions are deliberately about URLs and status codes rather
/// than about metadata, because that is what the reader is deciding between.
/// </para>
/// <para>
/// Anonymous only. What a signed-in viewer sees is the sign-in redirect doing its job, which
/// <c>samples/Sample.Own</c> exercises end to end against a browser; here the question is what
/// leaks before anyone signs in.
/// </para>
/// </remarks>
public class ReDocAuthorizationShapesTests
{
    private const string PublicPage = "/api-docs/public/";
    private const string PublicSpec = "/api-docs/public/openapi.json";
    private const string InternalPage = "/api-docs/internal/";
    private const string InternalSpec = "/api-docs/internal/openapi.json";
    private const string MappedPublicSpec = "/openapi/public.json";
    private const string MappedInternalSpec = "/openapi/internal.json";

    private static readonly IEnumerable<ReDocDocument> Documents =
        [new ReDocDocument("public"), new ReDocDocument("internal", Unlisted: true)];

    [Fact]
    public async Task EverythingPublic_IsTheDefaultAndGatesNothing()
    {
        await using ReDocTestApp app = await StartAsync(
            configureApp: web => web.MapOpenApi());

        // No ServeSpecs, no policy: the pages read the specs MapOpenApi serves, and all four URLs
        // answer anyone. Nothing about this shape changed when ServeSpecs was added.
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedPublicSpec));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task EverythingInternal_NeedsNoServeSpecs_BecauseOneEndpointServesEverySpec()
    {
        await using ReDocTestApp app = await StartAsync(
            configureApp: web => web.MapOpenApi().RequireAuthorization(),
            configureEndpoints: docs => docs.RequireAuthorization());

        // Both ends closed with two calls and no new option: when every document is internal,
        // MapOpenApi's one endpoint for all of them is exactly the right granularity.
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, MappedPublicSpec));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task Mixed_IsTheShapeThatNeedsServeSpecs()
    {
        await using ReDocTestApp app = await StartAsync(
            serveSpecs: true,
            configureEndpoints: docs => docs.ForDocument("internal").RequireAuthorization());

        // The public document keeps a public page and a public spec; the internal one has
        // neither. This is the row MapOpenApi can't express at all — it has no per-document seam,
        // so gating there is every spec or none.
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicSpec));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalSpec));

        // And the URL the old shape leaked through isn't mapped, because this shape doesn't map
        // MapOpenApi at all.
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task Mixed_WithMapOpenApiLeftOpen_StillLeaksTheInternalDocument()
    {
        await using ReDocTestApp app = await StartAsync(
            serveSpecs: true,
            configureApp: web => web.MapOpenApi(),
            configureEndpoints: docs => docs.ForDocument("internal").RequireAuthorization());

        // The mistake the README has to warn about, pinned here so it stays true: ServeSpecs gives
        // the page a copy its policy covers, and reaches nothing anyone else mapped. Deleting or
        // gating MapOpenApi is a second, deliberate step.
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalSpec));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedInternalSpec));
    }

    private static async Task<HttpStatusCode> StatusOf(ReDocTestApp app, string path)
    {
        HttpResponseMessage response = await app.GetAsync(path);
        return response.StatusCode;
    }

    private static Task<ReDocTestApp> StartAsync(
        bool serveSpecs = false,
        Action<WebApplication>? configureApp = null,
        Action<ReDocDocumentsConventionBuilder>? configureEndpoints = null) =>
        ReDocTestApp.StartAsync(
            Documents,
            options => options.ServeSpecs = serveSpecs,
            configureServices: builder =>
            {
                foreach (ReDocDocument document in Documents)
                {
                    builder.Services.AddOpenApi(document.Name);
                }

                // A cookie scheme, so an anonymous request redirects to a sign-in page rather than
                // answering 401 — the shape a browser-facing documentation site actually takes,
                // and the one that lets the spec fetch inherit the viewer's credential.
                builder.Services
                    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(cookie => cookie.LoginPath = "/sign-in");
                builder.Services.AddAuthorizationBuilder();
            },
            configureApp: web =>
            {
                web.UseAuthentication();
                web.UseAuthorization();
                configureApp?.Invoke(web);
            },
            configureEndpoints: configureEndpoints);
}
