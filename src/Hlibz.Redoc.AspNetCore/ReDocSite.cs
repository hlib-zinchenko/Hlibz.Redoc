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
        ReDocPrefixRedirect? redirect,
        string? contentSecurityPolicy,
        ReDocLastDocument? lastDocument)
    {
        Pages = pages;
        BundleRoute = bundleRoute;
        Redirect = redirect;
        ContentSecurityPolicy = contentSecurityPolicy;
        LastDocument = lastDocument;
    }

    public IReadOnlyList<ReDocSitePage> Pages { get; }

    /// <summary>The bundle's path relative to the app base, without a leading slash.</summary>
    public string BundleRoute { get; }

    public ReDocPrefixRedirect? Redirect { get; }

    /// <summary>The policy to send with each page, or <see langword="null"/> to send none.</summary>
    public string? ContentSecurityPolicy { get; }

    /// <summary>
    /// Where the prefix sends a returning viewer, or <see langword="null"/> when the site always
    /// sends them to the first document.
    /// </summary>
    public ReDocLastDocument? LastDocument { get; }

    /// <summary>
    /// Serves a page and, where the site remembers them, notes which document it was.
    /// </summary>
    public Task WritePage(HttpContext context, ReDocSitePage page)
    {
        LastDocument?.Remember(context, page.Name);
        return ReDocAssets.WritePage(context, page.Html, ContentSecurityPolicy);
    }

    /// <summary>
    /// Where a request for the route prefix should go: the document this viewer was last reading,
    /// or the site's first one.
    /// </summary>
    public string RedirectTargetFor(HttpContext context, ReDocPrefixRedirect redirect)
    {
        string? remembered = LastDocument?.RouteFor(context);
        string path = remembered switch
        {
            null => redirect.To,
            "" => "/",
            _ => $"/{remembered}/",
        };

        return $"{context.Request.PathBase}{path}";
    }

    public static ReDocSite Build(
        IServiceProvider services,
        IEnumerable<ReDocDocument> documents,
        ReDocDocumentsOptions options)
    {
        ReDocDocumentsPlan plan = ReDocDocumentsPlan.Build(services, documents, options);

        if (options.ServeSpecs
            && !string.Equals(
                options.SpecUrlTemplate,
                ReDocDocumentsOptionsBase.DefaultSpecUrlTemplate,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ServeSpecs and SpecUrlTemplate disagree about where the pages read their specs "
                + $"from: ServeSpecs serves each page's own at '{ReDocPage.SpecFileName}', while "
                + $"SpecUrlTemplate points at '{options.SpecUrlTemplate}'. Leave SpecUrlTemplate "
                + "alone, or set ReDocDocument.SpecUrl on the documents whose specs come from "
                + "somewhere else.");
        }

        string prefix = options.RoutePrefix.Trim('/');
        string bundleRoute = prefix.Length == 0
            ? $"{AssetFolder}/{ReDocBundle.FileName}"
            : $"{prefix}/{AssetFolder}/{ReDocBundle.FileName}";

        string colorModeScript = ReDocPage.ColorModeScript(options.DefaultColorMode);
        string configJson = ReDocPage.ConfigJson(options);

        // Two stylesheets at most, and usually one: whether a page carries the picker's rules
        // follows whether it carries the picker, which an unlisted document can make differ from
        // page to page.
        string? cssWithPicker = null;
        string? cssWithout = null;

        bool withToolbarItems = options.ToolbarItems.Count > 0;

        // The two controls are placed independently, so each script is built for its own strip.
        // Where the placements match, both resolve to the same container at runtime by class.
        ReDocToolbarSpot pickerSpot = ReDocPage.SpotOf(options.PickerPlacement);
        ReDocToolbarSpot itemsSpot = ReDocPage.SpotOf(options.ToolbarItemsPlacement);

        List<ReDocSitePage> pages = [];
        foreach (ReDocDocumentPage page in plan.Pages)
        {
            // Only worth a picker when that page has somewhere to go.
            bool withPicker = plan.WantsPicker(page);
            string css = withPicker
                ? cssWithPicker ??= ReDocPage.Css(options, withPicker: true)
                : cssWithout ??= ReDocPage.Css(options, withPicker: false);

            // A document that says where its spec is keeps reading it from there, whoever serves
            // it. Everything else is this page's to serve, or there is no page worth mounting.
            OpenApiSpecSource? spec = options.ServeSpecs && page.Document.SpecUrl is null
                ? OpenApiSpecSource.TryResolve(services, page.Document.Name)
                    ?? throw SpecUnavailable(page)
                : null;

            pages.Add(new ReDocSitePage(
                page.Route,
                page.Document.Name,
                ReDocPage.Html(
                    plan.DocumentTitleOf(page.Document), options, withPicker, withToolbarItems),
                css,
                ReDocPage.InitScript(
                    ReDocRelativePath.Between(page.Route, bundleRoute),
                    spec is null ? plan.SpecUrlOf(page.Document) : ReDocPage.SpecFileName,
                    configJson),
                colorModeScript,
                withPicker
                    ? plan.PickerScriptFor(page, pickerSpot, options.SearchablePicker)
                    : null,
                // Per page rather than once for the site: a `~/` item resolves against the page's
                // own route depth, the same way the picker's links do.
                withToolbarItems
                    ? ReDocPage.ToolbarScript(options.ToolbarItems, page.Route, itemsSpot)
                    : null,
                page.Document.Unlisted,
                spec));
        }

        return new ReDocSite(
            pages,
            bundleRoute,
            plan.Redirect,
            options.ContentSecurityPolicy,
            options.RememberLastDocument
                ? new ReDocLastDocument(pages, prefix.Length == 0 ? string.Empty : $"/{prefix}")
                : null);
    }

    /// <summary>
    /// Why a document can't serve its own spec, named as the thing the caller has to fix. Thrown
    /// at startup rather than left to the browser, which is where <c>ValidateDocumentNames</c>
    /// already draws that line.
    /// </summary>
    private static InvalidOperationException SpecUnavailable(ReDocDocumentPage page) =>
        new($"ServeSpecs is on, but no registered OpenAPI document provider offers a document "
            + $"named '{page.Document.Name}', so page '/{page.Route}/' would have no spec to "
            + "serve. Register it with AddOpenApi(\"" + page.Document.Name + "\") or Swashbuckle's "
            + "SwaggerDoc, set ReDocDocument.SpecUrl if its spec comes from somewhere else, or "
            + "turn ServeSpecs off and serve the specs yourself.");

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
            context.Response.Redirect(RedirectTargetFor(context, redirect));
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
                "" or "index.html" => WritePage(context, page),
                ReDocPage.StyleFileName => ReDocAssets.Write(context, page.Css, "text/css"),
                ReDocPage.InitFileName =>
                    ReDocAssets.Write(context, page.InitScript, "text/javascript"),
                ReDocPage.ColorModeFileName =>
                    ReDocAssets.Write(context, page.ColorModeScript, "text/javascript"),
                ReDocPage.PickerFileName when page.PickerScript is not null =>
                    ReDocAssets.Write(context, page.PickerScript, "text/javascript"),
                ReDocPage.ToolbarFileName when page.ToolbarScript is not null =>
                    ReDocAssets.Write(context, page.ToolbarScript, "text/javascript"),
                ReDocPage.SpecFileName when page.Spec is not null =>
                    ReDocAssets.WriteSpec(context, page.Spec),
                _ => next(context),
            };
        }

        return next(context);
    }
}

/// <summary>One document's page and the files it references, all built at startup.</summary>
internal sealed record ReDocSitePage(
    string Route,
    string Name,
    string Html,
    string Css,
    string InitScript,
    string ColorModeScript,
    string? PickerScript,
    string? ToolbarScript,
    bool Unlisted,
    OpenApiSpecSource? Spec)
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
