using Microsoft.AspNetCore.Http;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Everything a <c>MapReDocDocuments</c> or <c>UseReDocDocuments</c> call serves, worked out once
/// at startup: each page's markup, stylesheet and scripts, where the shared bundle lives, and the
/// prefix redirect.
/// </summary>
/// <remarks>
/// Nothing here is per-request, because nothing on these pages varies per request — the color mode
/// is the viewer's and is settled in the browser, and everything else is configuration. Building
/// it up front is also what lets a bad document name throw at startup rather than 404 later.
/// </remarks>
internal sealed class ReDocSite
{
    /// <summary>
    /// Where the shared bundle is served from, relative to the app base. One copy for every page,
    /// so a viewer switching documents doesn't download it again; the folder name is
    /// unlikely enough to collide with a document route, and <see cref="ReDocDocumentsPlan"/>
    /// would have rejected a duplicate route anyway.
    /// </summary>
    private const string AssetFolder = "_redoc";

    private ReDocSite(
        IReadOnlyList<ReDocSitePage> pages,
        string bundleRoute,
        ReDocPrefixRedirect? redirect)
    {
        Pages = pages;
        BundleRoute = bundleRoute;
        Redirect = redirect;
    }

    public IReadOnlyList<ReDocSitePage> Pages { get; }

    /// <summary>The bundle's path relative to the app base, without a leading slash.</summary>
    public string BundleRoute { get; }

    public ReDocPrefixRedirect? Redirect { get; }

    public static ReDocSite Build(
        IServiceProvider services,
        IEnumerable<ReDocDocument> documents,
        ReDocDocumentsOptions options)
    {
        ReDocDocumentsPlan plan = ReDocDocumentsPlan.Build(services, documents, options);

        string prefix = options.RoutePrefix.Trim('/');
        string bundleRoute = prefix.Length == 0
            ? $"{AssetFolder}/{ReDocBundle.FileName}"
            : $"{prefix}/{AssetFolder}/{ReDocBundle.FileName}";

        // Only worth a picker when there is somewhere to go.
        bool withPicker = plan.Pages.Count > 1 || options.Links.Count > 0;
        string colorModeScript = ReDocPage.ColorModeScript(options.DefaultColorMode);
        string css = ReDocPage.Css(options, withPicker);
        string configJson = ReDocPage.ConfigJson(options);

        List<ReDocSitePage> pages = [];
        foreach (ReDocDocumentPage page in plan.Pages)
        {
            pages.Add(new ReDocSitePage(
                page.Route,
                ReDocPage.Html(plan.DocumentTitleOf(page.Document), options, withPicker),
                css,
                ReDocPage.InitScript(
                    ReDocRelativePath.Between(page.Route, bundleRoute),
                    plan.SpecUrlOf(page.Document),
                    configJson),
                colorModeScript,
                withPicker ? plan.PickerScriptFor(page) : null));
        }

        return new ReDocSite(pages, bundleRoute, plan.Redirect);
    }

    /// <summary>
    /// The middleware form: match a request against the site's routes by path, because without
    /// endpoint routing there is nothing else to match on.
    /// </summary>
    public Task Handle(HttpContext context, RequestDelegate next)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return next(context);
        }

        string path = (context.Request.Path.Value ?? string.Empty).TrimStart('/');

        if (Redirect is { } redirect && redirect.Matches(context.Request.Path))
        {
            context.Response.Redirect($"{context.Request.PathBase}{redirect.To}");
            return Task.CompletedTask;
        }

        if (string.Equals(path, BundleRoute, StringComparison.Ordinal))
        {
            return ReDocAssets.WriteBundle(context);
        }

        foreach (ReDocSitePage page in Pages)
        {
            if (!page.Owns(path, out string? file))
            {
                continue;
            }

            if (file is null)
            {
                // The page's folder without its trailing slash. Redirecting rather than serving
                // keeps every relative href on the page resolving against the folder.
                context.Response.Redirect($"{page.LastSegment}/");
                return Task.CompletedTask;
            }

            return file switch
            {
                "" or "index.html" => ReDocAssets.Write(context, page.Html, "text/html"),
                ReDocPage.StyleFileName => ReDocAssets.Write(context, page.Css, "text/css"),
                ReDocPage.InitFileName =>
                    ReDocAssets.Write(context, page.InitScript, "text/javascript"),
                ReDocPage.ColorModeFileName =>
                    ReDocAssets.Write(context, page.ColorModeScript, "text/javascript"),
                ReDocPage.PickerFileName when page.PickerScript is not null =>
                    ReDocAssets.Write(context, page.PickerScript, "text/javascript"),
                _ => next(context),
            };
        }

        return next(context);
    }
}

/// <summary>One document's page and the files it references, all built at startup.</summary>
internal sealed record ReDocSitePage(
    string Route,
    string Html,
    string Css,
    string InitScript,
    string ColorModeScript,
    string? PickerScript)
{
    /// <summary>
    /// The last segment of the route, which is what the page's own folder is called — the target
    /// of the redirect from the folder without its trailing slash.
    /// </summary>
    public string LastSegment => Route[(Route.LastIndexOf('/') + 1)..];

    /// <summary>
    /// Whether a request path falls inside this page's folder, and if so which file it asks for:
    /// empty for the folder itself, and <see langword="null"/> for the folder without its
    /// trailing slash, which wants a redirect rather than a response.
    /// </summary>
    public bool Owns(string path, out string? file)
    {
        file = null;
        if (Route.Length == 0)
        {
            file = path;
            return !file.Contains('/', StringComparison.Ordinal);
        }

        if (!path.StartsWith(Route, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = path[Route.Length..];
        if (rest.Length == 0)
        {
            return true;
        }

        if (rest[0] != '/')
        {
            return false;
        }

        file = rest[1..];
        return !file.Contains('/', StringComparison.Ordinal);
    }
}
