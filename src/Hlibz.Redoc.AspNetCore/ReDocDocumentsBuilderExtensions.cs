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
    /// <c>.CacheOutput()</c> and friends apply to all of them together.
    /// </returns>
    /// <remarks>
    /// Unlike the <see cref="UseReDocDocuments"/> middleware, endpoints carry metadata, which is
    /// the only way to put a documentation page behind a policy.
    /// </remarks>
    public static IEndpointConventionBuilder MapReDocDocuments(
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

        // Every handler is a RequestDelegate rather than a lambda with inferred parameters: the
        // Delegate overloads of MapGet reflect over their arguments, which this assembly's
        // IsAotCompatible promise doesn't allow.
        foreach (ReDocSitePage page in site.Pages)
        {
            ReDocSitePage current = page;
            string route = current.Route.Length == 0 ? string.Empty : $"/{current.Route}";

            // A route pattern ignores its trailing slash, so this one endpoint also matches the
            // folder without it — which can't be a second endpoint, and mustn't serve the page
            // either, or every relative href on it would resolve against the parent folder.
            added.Add(endpoints.MapGet($"{route}/", context =>
            {
                if (route.Length > 0 && !context.Request.Path.Value!.EndsWith('/'))
                {
                    context.Response.Redirect($"{current.LastSegment}/", permanent: false);
                    return Task.CompletedTask;
                }

                return ReDocAssets.Write(context, current.Html, "text/html");
            }));
            added.Add(endpoints.MapGet(
                $"{route}/index.html",
                context => ReDocAssets.Write(context, current.Html, "text/html")));
            added.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.StyleFileName}",
                context => ReDocAssets.Write(context, current.Css, "text/css")));
            added.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.InitFileName}",
                context => ReDocAssets.Write(context, current.InitScript, "text/javascript")));
            added.Add(endpoints.MapGet(
                $"{route}/{ReDocPage.ColorModeFileName}",
                context => ReDocAssets.Write(context, current.ColorModeScript, "text/javascript")));
            if (current.PickerScript is { } picker)
            {
                added.Add(endpoints.MapGet(
                    $"{route}/{ReDocPage.PickerFileName}",
                    context => ReDocAssets.Write(context, picker, "text/javascript")));
            }

        }

        added.Add(endpoints.MapGet($"/{site.BundleRoute}", ReDocAssets.WriteBundle));

        if (site.Redirect is { } redirect)
        {
            added.Add(endpoints.MapGet(
                redirect.From.Length == 0 ? "/" : redirect.From,
                context =>
                {
                    context.Response.Redirect(
                        $"{context.Request.PathBase}{redirect.To}", permanent: false);
                    return Task.CompletedTask;
                }));
        }

        return new CompositeConventionBuilder(added);
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

    private sealed class CompositeConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> parts)
        : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            foreach (IEndpointConventionBuilder part in parts)
            {
                part.Add(convention);
            }
        }

        public void Finally(Action<EndpointBuilder> finalConvention)
        {
            foreach (IEndpointConventionBuilder part in parts)
            {
                part.Finally(finalConvention);
            }
        }
    }
}
