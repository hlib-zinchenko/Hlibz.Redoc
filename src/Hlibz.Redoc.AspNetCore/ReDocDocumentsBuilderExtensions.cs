using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Extension methods for serving several OpenAPI documents as ReDoc pages with a picker, from an
/// embedded ReDoc bundle and this package's own page.
/// </summary>
public static class ReDocDocumentsBuilderExtensions
{
    /// <summary>
    /// Maps one ReDoc page per document, by default at <c>/{RoutePrefix}/{Name}/</c>, each with a
    /// picker for switching between them, and redirects <c>/{RoutePrefix}</c> to the first one.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="documents">The documents to mount, in picker order.</param>
    /// <param name="configure">Route, redirect, spec URL, extra links and page settings.</param>
    /// <returns>
    /// A convention builder covering every endpoint this call adds — the pages, their stylesheet
    /// and scripts, and the shared ReDoc bundle — so <c>.RequireAuthorization()</c>,
    /// <c>.CacheOutput()</c> and friends apply to all of them together, plus
    /// <see cref="ReDocDocumentsConventionBuilder.ForDocument"/> for one document on its own.
    /// </returns>
    /// <remarks>
    /// Unlike the <see cref="UseReDocDocuments"/> middleware, endpoints carry metadata, which is
    /// the only way to put a documentation page behind a policy.
    /// </remarks>
    public static ReDocDocumentsConventionBuilder MapReDocDocuments(
        this IEndpointRouteBuilder endpoints,
        IEnumerable<ReDocDocument> documents,
        Action<ReDocDocumentsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(documents);

        ReDocDocumentsOptions options = new();
        configure?.Invoke(options);

        ReDocSite site = ReDocSite.Build(endpoints.ServiceProvider, documents, options);
        List<IEndpointConventionBuilder> added = [];

        // Per document as well as in one list: the shared endpoints below belong to the site, not
        // to any one document, so they are in `added` and in no group.
        Dictionary<string, ReDocDocumentsConventionBuilder.Group> byDocument =
            new(StringComparer.OrdinalIgnoreCase);

        // Every handler is a RequestDelegate rather than a lambda with inferred parameters: the
        // Delegate overloads of MapGet reflect over their arguments, which this assembly's
        // IsAotCompatible promise doesn't allow.
        foreach (ReDocSitePage page in site.Pages)
        {
            ReDocSitePage current = page;
            string route = current.Route.Length == 0 ? string.Empty : $"/{current.Route}";
            List<IEndpointConventionBuilder> own = [];

            // A route pattern ignores its trailing slash, so this one endpoint also matches the
            // folder without it — which can't be a second endpoint, and mustn't serve the page
            // either, or every relative href on it would resolve against the parent folder.
            own.Add(endpoints.MapGet($"{route}/", context =>
            {
                if (route.Length > 0 && !context.Request.Path.Value!.EndsWith('/'))
                {
                    context.Response.Redirect($"{current.LastSegment}/", permanent: false);
                    return Task.CompletedTask;
                }

                return site.WritePage(context, current);
            }));
            own.Add(endpoints.MapGet(
                $"{route}/index.html",
                context => site.WritePage(context, current)));
            own.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.StyleFileName}",
                context => ReDocAssets.Write(context, current.Css, "text/css")));
            own.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.InitFileName}",
                context => ReDocAssets.Write(context, current.InitScript, "text/javascript")));
            own.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.ColorModeFileName}",
                context => ReDocAssets.Write(context, current.ColorModeScript, "text/javascript")));
            if (current.PickerScript is { } picker)
            {
                own.Add(endpoints.MapGet(
                    $"{route}/{ReDocPage.PickerFileName}",
                    context => ReDocAssets.Write(context, picker, "text/javascript")));
            }

            // In this document's own group, which is the whole point: one ForDocument(...)
            // .RequireAuthorization() then covers the page and the document it renders.
            if (current.Spec is { } spec)
            {
                own.Add(endpoints.MapGet(
                    $"{route}/{ReDocPage.SpecFileName}",
                    context => ReDocAssets.WriteSpec(context, spec)));
            }

            if (current.ToolbarScript is { } toolbar)
            {
                own.Add(endpoints.MapGet(
                    $"{route}/{ReDocPage.ToolbarFileName}",
                    context => ReDocAssets.Write(context, toolbar, "text/javascript")));
            }

            added.AddRange(own);
            byDocument[current.Name] = new ReDocDocumentsConventionBuilder.Group(own);
        }

        added.Add(endpoints.MapGet($"/{site.BundleRoute}", ReDocAssets.WriteBundle));

        if (site.Redirect is { } redirect)
        {
            added.Add(endpoints.MapGet(
                redirect.From.Length == 0 ? "/" : redirect.From,
                context =>
                {
                    context.Response.Redirect(
                        site.RedirectTargetFor(context, redirect), permanent: false);
                    return Task.CompletedTask;
                }));
        }

        return new ReDocDocumentsConventionBuilder(added, byDocument);
    }

    /// <summary>
    /// The middleware form of <see cref="MapReDocDocuments"/>, for an app that isn't using
    /// endpoint routing. It serves the same pages, but nothing can attach authorization or
    /// caching metadata to them — prefer <see cref="MapReDocDocuments"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="documents">The documents to mount, in picker order.</param>
    /// <param name="configure">Route, redirect, spec URL, extra links and page settings.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IApplicationBuilder UseReDocDocuments(
        this IApplicationBuilder app,
        IEnumerable<ReDocDocument> documents,
        Action<ReDocDocumentsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(documents);

        ReDocDocumentsOptions options = new();
        configure?.Invoke(options);

        ReDocSite site = ReDocSite.Build(app.ApplicationServices, documents, options);
        return app.Use((context, next) => site.Handle(context, next));
    }

}
