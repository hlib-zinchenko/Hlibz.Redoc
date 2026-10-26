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
    private const string PublicPicker = "/api-docs/public/redoc-picker.js";
    private const string InternalPage = "/api-docs/internal/";
    private const string InternalSpec = "/api-docs/internal/openapi.json";
    private const string MappedPublicSpec = "/openapi/public.json";
    private const string MappedInternalSpec = "/openapi/internal.json";

    /// <summary>Two plain documents: whatever gating happens, happens at the call site.</summary>
    private static IEnumerable<ReDocDocument> Open =>
        [new ReDocDocument("public"), new ReDocDocument("internal")];

    /// <summary>The mixed shape, which is one word on the one document.</summary>
    private static IEnumerable<ReDocDocument> Mixed =>
        [new ReDocDocument("public"), new ReDocDocument("internal").RequireAuthorization()];

    [Fact]
    public async Task EverythingPublic_IsTheDefaultAndGatesNothing()
    {
        await using ReDocTestApp app = await StartAsync(
            Open,
            configureApp: web => web.MapOpenApi());

        // No policy anywhere: the pages read the specs MapOpenApi serves, and all four URLs answer
        // anyone. Nothing about this shape changed when per-document authorization arrived.
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedPublicSpec));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task EverythingInternal_NeedsNothingPerDocument()
    {
        await using ReDocTestApp app = await StartAsync(
            Open,
            configureApp: web => web.MapOpenApi().RequireAuthorization(),
            configureEndpoints: docs => docs.RequireAuthorization());

        // Both ends closed with two calls and nothing on the documents: when every document is
        // internal, MapOpenApi's one endpoint for all of them is exactly the right granularity.
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, MappedPublicSpec));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task Mixed_IsOneCallOnTheDocumentItself()
    {
        await using ReDocTestApp app = await StartAsync(Mixed);

        // The public document keeps a public page and a public spec; the internal one has neither.
        // This is the row MapOpenApi can't express at all — it has no per-document seam, so gating
        // there is every spec or none.
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicPage));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, PublicSpec));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalPage));
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalSpec));

        // Nothing else was written: no ServeSpecs, no MapOpenApi, and so no URL of the kind the
        // old shape leaked through.
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(app, MappedPublicSpec));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(app, MappedInternalSpec));
    }

    [Fact]
    public async Task Mixed_DoesntNameTheInternalDocumentOnThePublicPage()
    {
        await using ReDocTestApp app = await StartAsync(Mixed);

        string page = await app.GetStringAsync(PublicPage);

        // The third thing one word does. With the internal document hidden, the public page has
        // nowhere left to go, so it carries no picker at all — rather than a dropdown naming an
        // API this viewer can't open.
        Assert.DoesNotContain("redoc-picker.js", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(app, PublicPicker));

        // And the internal page, being hidden itself, still lists everything — so a viewer who
        // signs in can navigate back. Its picker can't be read from here, but 302 rather than 404
        // says the endpoint exists and the policy is what turned it away: the page was built with
        // a picker, and the public page above was not.
        Assert.Equal(
            HttpStatusCode.Redirect, await StatusOf(app, "/api-docs/internal/redoc-picker.js"));
    }

    [Fact]
    public async Task Mixed_WithMapOpenApiLeftOpen_StillLeaksTheInternalDocument()
    {
        await using ReDocTestApp app = await StartAsync(
            Mixed,
            configureApp: web => web.MapOpenApi());

        // The mistake the README has to warn about, pinned here so it stays true: gating a
        // document covers the copy this package serves, and reaches nothing anyone else mapped.
        // Deleting or gating MapOpenApi is a second, deliberate step — and with a gated document
        // in the call there is no longer any reason to have mapped it.
        Assert.Equal(HttpStatusCode.Redirect, await StatusOf(app, InternalSpec));
        Assert.Equal(HttpStatusCode.OK, await StatusOf(app, MappedInternalSpec));
    }

    private static async Task<HttpStatusCode> StatusOf(ReDocTestApp app, string path)
    {
        HttpResponseMessage response = await app.GetAsync(path);
        return response.StatusCode;
    }

    private static Task<ReDocTestApp> StartAsync(
        IEnumerable<ReDocDocument> documents,
        Action<WebApplication>? configureApp = null,
        Action<ReDocDocumentsConventionBuilder>? configureEndpoints = null)
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
}
