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
        return ReDocAssets.WritePage(
            context, page.Html, page.CacheControl, ContentSecurityPolicy);
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

    // `caller` is the public extension method that led here — MapReDocDocuments or
    // UseReDocDocuments — so a startup failure names the call the consumer actually wrote.
    public static ReDocSite Build(
        IServiceProvider services,
        IEnumerable<ReDocDocument> documents,
        ReDocDocumentsOptions options,
        string caller)
    {
        ReDocDocumentsPlan plan = ReDocDocumentsPlan.Build(services, documents, options, caller);

        // A gated document serves its own spec whether or not ServeSpecs is on — a policy on these
        // endpoints can only cover a spec these endpoints serve — so it meets the same conflict.
        ReDocDocumentPage? gatedPage =
            plan.Pages.FirstOrDefault(page => page.Document is ReDocGatedDocument);

        if (!string.Equals(
            options.SpecUrlTemplate,
            ReDocDocumentsOptionsBase.DefaultSpecUrlTemplate,
            StringComparison.Ordinal))
        {
            if (options.ServeSpecs)
            {
                throw new InvalidOperationException(
                    "ServeSpecs and SpecUrlTemplate disagree about where the pages read their "
                    + $"specs from: ServeSpecs serves each page's own at "
                    + $"'{ReDocPage.SpecFileName}', while SpecUrlTemplate points at "
                    + $"'{options.SpecUrlTemplate}'. Leave SpecUrlTemplate alone, or set "
                    + "ReDocDocument.SpecUrl on the documents whose specs come from somewhere "
                    + "else.");
            }

            if (gatedPage is { } conflicting)
            {
                throw new InvalidOperationException(
                    $"Document '{conflicting.Document.Name}' requires authorization, so its page "
                    + $"serves that document itself at '{ReDocPage.SpecFileName}'. But "
                    + $"SpecUrlTemplate points the pages at '{options.SpecUrlTemplate}', and only "
                    + "one of those can be where a page reads its spec. Leave SpecUrlTemplate "
                    + "alone, or drop RequireAuthorization on the document and gate it yourself "
                    + $"with MapReDocDocuments(...).ForDocument(\"{conflicting.Document.Name}\").");
            }
        }

        // One gated document puts the whole site in "we serve the specs" mode, rather than only
        // that document's page. A gated page has to serve its own spec for the policy to mean
        // anything, and once it does, the other pages can't fall back on MapOpenApi either: that
        // endpoint serves every document, the gated one included, so a site still needing it for
        // its public pages would be a site with the lock and the open window both. Which would
        // leave gating a document a two-call affair again, for no reason the caller could see.
        bool serveSpecs = options.ServeSpecs || gatedPage is not null;
        string specsBecause = options.ServeSpecs
            ? "ServeSpecs is on"
            : $"document '{gatedPage?.Document.Name}' requires authorization, so every page "
                + "serves its own spec";

        // Resolved once: a TimeProvider registered by the app (a test's, usually) is the clock the
        // spec cache expires by.
        TimeProvider time =
            (TimeProvider?)services.GetService(typeof(TimeProvider)) ?? TimeProvider.System;

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

        // One encoded file per distinct content, however many pages share it — the stylesheet and
        // the color-mode script usually are the same on every page, and each copy would otherwise
        // be compressed and held separately.
        Dictionary<string, ReDocFile> files = new(StringComparer.Ordinal);
        ReDocFile FileOf(string content, string contentType)
        {
            if (!files.TryGetValue(content, out ReDocFile? file))
            {
                file = new ReDocFile(content, contentType);
                files[content] = file;
            }

            return file;
        }

        // Versioned, because the bundle is sent immutable: an upgraded package has to be a new URL
        // or viewers keep the old bundle for a year. The query string is ignored when matching.
        string BundleHref(string route) =>
            $"{ReDocRelativePath.Between(route, bundleRoute)}?v={ReDocBundle.UrlVersion}";

        bool withToolbarItems = options.ToolbarItems.Count > 0;

        // The two controls are placed independently, so each script is built for its own strip.
        // Where the placements match, both resolve to the same container at runtime by class.
        ReDocToolbarSpot pickerSpot = ReDocPage.SpotOf(options.PickerPlacement);
        ReDocToolbarSpot itemsSpot = ReDocPage.SpotOf(options.ToolbarItemsPlacement);

        List<ReDocSitePage> pages = [];
        SortedSet<string> specOrigins = new(StringComparer.Ordinal);
        foreach (ReDocDocumentPage page in plan.Pages)
        {
            // Only worth a picker when that page has somewhere to go.
            bool withPicker = plan.WantsPicker(page);
            string css = withPicker
                ? cssWithPicker ??= ReDocPage.Css(options, withPicker: true)
                : cssWithout ??= ReDocPage.Css(options, withPicker: false);

            IReadOnlyList<string>? policies =
                (page.Document as ReDocGatedDocument)?.Policies;

            // A gated document pointed at a spec elsewhere would be a locked page in front of an
            // open document, which is the one outcome this whole feature exists to rule out.
            if (policies is not null && page.Document.SpecUrl is { } elsewhere)
            {
                throw new InvalidOperationException(
                    $"Document '{page.Document.Name}' requires authorization, but its SpecUrl "
                    + $"points at '{elsewhere}', which this package doesn't serve and a policy "
                    + "here cannot cover — the page would be gated while the document stayed "
                    + "open. Remove SpecUrl so the page serves the document itself, or drop "
                    + "RequireAuthorization and gate both ends yourself with "
                    + $"MapReDocDocuments(...).ForDocument(\"{page.Document.Name}\").");
            }

            // A document that says where its spec is keeps reading it from there, whoever serves
            // it. Everything else is this page's to serve, or there is no page worth mounting.
            OpenApiSpecSource? spec = serveSpecs && page.Document.SpecUrl is null
                ? OpenApiSpecSource.TryResolve(services, page.Document.Name)
                    ?? throw SpecUnavailable(page, specsBecause)
                : null;

            string specUrl = spec is null ? plan.SpecUrlOf(page.Document) : ReDocPage.SpecFileName;
            if (CrossOriginSource(specUrl) is { } origin)
            {
                specOrigins.Add(origin);
            }

            pages.Add(new ReDocSitePage(
                page.Route,
                page.Document.Name,
                FileOf(
                    ReDocPage.Html(
                        plan.DocumentTitleOf(page.Document),
                        options,
                        withPicker,
                        withToolbarItems,
                        options.TryIt is { } tryIt
                            ? ReDocTryIt.Tag(specUrl, page.Route, tryIt)
                            : null),
                    "text/html"),
                FileOf(css, "text/css"),
                FileOf(
                    ReDocPage.InitScript(
                        BundleHref(page.Route),
                        specUrl,
                        configJson,
                        shareSpec: options.TryIt is not null),
                    "text/javascript"),
                FileOf(colorModeScript, "text/javascript"),
                withPicker
                    ? FileOf(
                        plan.PickerScriptFor(page, pickerSpot, options.SearchablePicker),
                        "text/javascript")
                    : null,
                // Per page rather than once for the site: a `~/` item resolves against the page's
                // own route depth, the same way the picker's links do.
                withToolbarItems
                    ? FileOf(
                        ReDocPage.ToolbarScript(options.ToolbarItems, page.Route, itemsSpot),
                        "text/javascript")
                    : null,
                page.Document.Unlisted,
                spec,
                policies,
                options.TryIt is not null,
                // Never for a gated document, whose spec may differ by who is asking. One gated
                // through the convention builder instead is caught per request, by its metadata.
                spec is not null
                    && policies is null
                    && options.SpecCacheDuration is { } cacheFor
                        ? new ReDocSpecCache(spec, cacheFor, time)
                        : null));
        }

        // A setting that silently did nothing would be read as working.
        if (options.SpecCacheDuration is not null && pages.All(page => page.Spec is null))
        {
            throw new InvalidOperationException(
                "SpecCacheDuration caches the specs these pages serve, but none of them serves "
                + "one: the pages read their specs from SpecUrlTemplate or SpecUrl. Turn on "
                + "ServeSpecs, or remove SpecCacheDuration.");
        }

        return new ReDocSite(
            pages,
            bundleRoute,
            plan.Redirect,
            PolicyFor(options, specOrigins),
            options.RememberLastDocument
                ? new ReDocLastDocument(pages, prefix.Length == 0 ? string.Empty : $"/{prefix}")
                : null);
    }

    /// <summary>
    /// The policy the pages are sent with. A try-it console that keeps credentials in browser
    /// storage gets <see cref="ReDocDocumentsOptions.DefaultContentSecurityPolicy"/> when nothing
    /// else was set: a stored secret is exactly what an injected script would come for, and
    /// <c>script-src 'self'</c> is the defence that keeps one off the page. A policy the caller set
    /// here, or one their own middleware put on the response, still wins.
    /// </summary>
    /// <remarks>
    /// The default allows <c>connect-src 'self'</c> only, so imposed as it stands it would block
    /// ReDoc's own fetch of a spec on another origin and blank a page that worked before the
    /// console was turned on. Those origins are known here, so they are added. A <c>servers</c>
    /// entry on another origin is not: the spec is read in the browser, and the README says to set
    /// <see cref="ReDocDocumentsOptions.ContentSecurityPolicy"/> for it.
    /// </remarks>
    private static string? PolicyFor(
        ReDocDocumentsOptions options, IReadOnlyCollection<string> specOrigins)
    {
        if (options.ContentSecurityPolicy is not null)
        {
            return options.ContentSecurityPolicy;
        }

        if (options.TryIt is not { CredentialStorage: not ReDocTryItCredentialStorage.Memory })
        {
            return null;
        }

        string policy = ReDocDocumentsOptions.DefaultContentSecurityPolicy;
        return specOrigins.Count == 0
            ? policy
            : policy.Replace(
                "connect-src 'self'",
                $"connect-src 'self' {string.Join(' ', specOrigins)}",
                StringComparison.Ordinal);
    }

    /// <summary>
    /// The CSP source a spec URL on another origin needs, or <see langword="null"/> for a relative
    /// one. A protocol-relative URL becomes a bare host, which CSP matches on the page's own
    /// scheme — the same scheme the URL itself would use.
    /// </summary>
    internal static string? CrossOriginSource(string specUrl)
    {
        if (specUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return Uri.TryCreate("https:" + specUrl, UriKind.Absolute, out Uri? relative)
                ? relative.Authority
                : null;
        }

        // Scheme checked rather than trusted: on Unix a rooted path parses as an absolute file URI.
        return Uri.TryCreate(specUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.GetLeftPart(UriPartial.Authority)
                : null;
    }

    /// <summary>
    /// Why a document can't serve its own spec, named as the thing the caller has to fix. Thrown
    /// at startup rather than left to the browser, which is where <c>ValidateDocumentNames</c>
    /// already draws that line.
    /// </summary>
    /// <param name="page">The page that would have nothing to serve.</param>
    /// <param name="because">
    /// Why this page is serving its own spec at all — <c>ServeSpecs</c>, or a gated document
    /// somewhere in the same call. Named in the message, so the caller can tell which of the two
    /// settings to go and look at.
    /// </param>
    private static InvalidOperationException SpecUnavailable(
        ReDocDocumentPage page, string because) =>
        new($"{because}, but no registered OpenAPI document provider offers a document named "
            + $"'{page.Document.Name}', so page '/{page.Route}/' would have no spec to serve. "
            + $"Register it with AddOpenApi(\"{page.Document.Name}\") or Swashbuckle's SwaggerDoc, "
            + "or set ReDocDocument.SpecUrl if its spec comes from somewhere else.");

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
                ReDocPage.StyleFileName => page.Css.WriteAsync(context, page.CacheControl),
                ReDocPage.InitFileName => page.InitScript.WriteAsync(context, page.CacheControl),
                ReDocPage.ColorModeFileName =>
                    page.ColorModeScript.WriteAsync(context, page.CacheControl),
                ReDocPage.PickerFileName when page.PickerScript is not null =>
                    page.PickerScript.WriteAsync(context, page.CacheControl),
                ReDocPage.ToolbarFileName when page.ToolbarScript is not null =>
                    page.ToolbarScript.WriteAsync(context, page.CacheControl),
                ReDocPage.SpecFileName when page.Spec is not null =>
                    ReDocAssets.WriteSpec(context, page.Spec, page.SpecCache),
                ReDocTryIt.FileName when page.TryIt =>
                    ReDocTryIt.File.WriteAsync(context, page.CacheControl),
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
    ReDocFile Html,
    ReDocFile Css,
    ReDocFile InitScript,
    ReDocFile ColorModeScript,
    ReDocFile? PickerScript,
    ReDocFile? ToolbarScript,
    bool Unlisted,
    OpenApiSpecSource? Spec,
    IReadOnlyList<string>? Policies,
    bool TryIt = false,
    ReDocSpecCache? SpecCache = null)
{
    /// <summary>
    /// What this page's files are sent with: always revalidated, and kept out of shared caches
    /// altogether when the document is gated, since the next person through a proxy isn't
    /// necessarily someone the policy lets in.
    /// </summary>
    public string CacheControl => Policies is null ? "no-cache" : "private, no-cache";

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
