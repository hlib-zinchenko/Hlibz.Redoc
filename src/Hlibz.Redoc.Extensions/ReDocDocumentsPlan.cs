using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Everything <c>UseReDocDocuments</c> works out before it touches a host: the validated set of
/// pages and their routes, each page's spec URL, title and picker markup, and the prefix redirect.
/// </summary>
/// <remarks>
/// <para>
/// Only the per-page <c>UseReDoc</c> call is host-specific, so an adapter is left with building
/// its options, calling
/// <see cref="Build(IApplicationBuilder, IEnumerable{ReDocDocument}, ReDocDocumentsOptionsBase, string)"/>,
/// and looping <see cref="Pages"/>. Everything in here
/// moved out of the original single-host <c>ReDocDocumentsBuilderExtensions</c> unchanged,
/// including the exception messages and their <c>paramName</c>s, which name the public extension
/// method's parameters rather than this type's.
/// </para>
/// <para>
/// Routes are computed up front, before the first page is built, because every host's
/// <c>UseReDoc</c> runs its setup action immediately and each page's picker links to all of them.
/// </para>
/// </remarks>
internal sealed class ReDocDocumentsPlan
{
    private readonly ReDocDocumentsOptionsBase options;
    private readonly IReadOnlyList<ReDocLink> links;

    private readonly IReadOnlyList<ReDocDocumentPage> listedPages;

    private ReDocDocumentsPlan(
        IReadOnlyList<ReDocDocumentPage> pages,
        IReadOnlyList<ReDocLink> links,
        ReDocDocumentsOptionsBase options,
        ReDocPrefixRedirect? redirect)
    {
        Pages = pages;
        listedPages = pages.Any(page => page.Document.Unlisted)
            ? [.. pages.Where(page => !page.Document.Unlisted)]
            : pages;
        this.links = links;
        this.options = options;
        Redirect = redirect;
    }

    /// <summary>The pages to mount, in picker order.</summary>
    public IReadOnlyList<ReDocDocumentPage> Pages { get; }

    /// <summary>
    /// The redirect from the route prefix to the first page, or <see langword="null"/> when none
    /// is wanted. The adapters have it registered for them by
    /// <see cref="Build(IApplicationBuilder, IEnumerable{ReDocDocument}, ReDocDocumentsOptionsBase, string)"/>;
    /// a host that owns its routing maps it itself.
    /// </summary>
    public ReDocPrefixRedirect? Redirect { get; }

    /// <summary>
    /// Validates the documents, computes their routes, registers the prefix redirect when one is
    /// wanted, and checks the document names against the app's registered OpenAPI documents.
    /// </summary>
    // `caller` is the public extension method the consumer called, and it is there for the
    // exception messages: a reader told to fix their UseReDocDocuments call when they wrote
    // MapReDocDocuments has to work out for themselves that the two are the same thing.
    public static ReDocDocumentsPlan Build(
        IApplicationBuilder app,
        IEnumerable<ReDocDocument> documents,
        ReDocDocumentsOptionsBase options,
        string caller)
    {
        ReDocDocumentsPlan plan = Build(app.ApplicationServices, documents, options, caller);
        if (plan.Redirect is { } redirect)
        {
            UseRedirect(app, redirect);
        }

        return plan;
    }

    /// <summary>
    /// The same, without touching the pipeline: for a host that maps its own endpoints and wants
    /// <see cref="Redirect"/> as data rather than as middleware.
    /// </summary>
    public static ReDocDocumentsPlan Build(
        IServiceProvider services,
        IEnumerable<ReDocDocument> documents,
        ReDocDocumentsOptionsBase options,
        string caller)
    {
        string prefix = options.RoutePrefix.Trim('/');
        IReadOnlyList<ReDocDocument> docs = [.. documents];
        IReadOnlyList<ReDocLink> links = [.. options.Links];
        Validate(docs, links);

        IReadOnlyList<ReDocDocumentPage> pages =
            [.. docs.Select(doc => new ReDocDocumentPage(doc, RouteOf(doc, prefix)))];
        ValidateRoutes(pages);

        bool prefixIsAPage = pages.Any(
            page => string.Equals(page.Route, prefix, StringComparison.OrdinalIgnoreCase));
        ReDocPrefixRedirect? redirect = options.RedirectToFirstDocument && !prefixIsAPage
            ? new ReDocPrefixRedirect(
                prefix.Length == 0 ? string.Empty : $"/{prefix}",
                pages[0].Route.Length == 0 ? "/" : $"/{pages[0].Route}/")
            : null;

        ReDocDocumentsPlan plan = new(pages, links, options, redirect);
        plan.ValidateDocumentNames(services, caller);
        return plan;
    }

    /// <summary>
    /// The spec URL for a document: its own, or the template with its name filled in.
    /// </summary>
    public string SpecUrlOf(ReDocDocument doc) =>
        doc.SpecUrl ?? options.SpecUrlTemplate.Replace("{documentName}", doc.Name);

    /// <summary>
    /// The document's title, HTML-encoded: every host so far writes it into <c>&lt;title&gt;</c>
    /// as-is.
    /// </summary>
    public string DocumentTitleOf(ReDocDocument doc) =>
        WebUtility.HtmlEncode(doc.DisplayName ?? doc.Name);

    /// <summary>
    /// The pages one page's picker lists: all of them from an unlisted page, and only the listed
    /// ones from a listed page.
    /// </summary>
    /// <remarks>
    /// Which entries a picker shows is decided here, at startup, and not per request — the page is
    /// one string built once, and making it depend on who is asking would cost that. So the rule
    /// is about the page doing the asking, not about its viewer: a page nobody is meant to find
    /// shows everything, a page anyone can open shows only what anyone can open. The current page
    /// is in its own list either way, which the script relies on.
    /// </remarks>
    public IReadOnlyList<ReDocDocumentPage> PickerPagesFrom(ReDocDocumentPage page) =>
        page.Document.Unlisted ? Pages : listedPages;

    /// <summary>Whether a page has anywhere to go, and so wants a picker at all.</summary>
    public bool WantsPicker(ReDocDocumentPage page) =>
        PickerPagesFrom(page).Count > 1 || links.Count > 0;

    /// <summary>The picker markup for one page, to append to that page's head content.</summary>
    public string PickerFor(ReDocDocumentPage page) =>
        ReDocDocumentPicker.Build(PickerPagesFrom(page), links, page);

    /// <summary>
    /// The picker's script for one page, without the <c>&lt;script&gt;</c> tags: for a host that
    /// serves it as a file instead of inlining it into the page.
    /// </summary>
    /// <param name="page">The page the script is served from.</param>
    /// <param name="spot">
    /// Which control strip the picker goes into. Only a host that owns its page has the choice;
    /// the adapters take <see cref="ReDocToolbarSpot.Shared"/>.
    /// </param>
    /// <param name="searchable">
    /// Whether to serve the searchable picker rather than the native <c>&lt;select&gt;</c>. Host
    /// only: it needs CSS of its own, and the adapters can only reach the page through
    /// <c>HeadContent</c>.
    /// </param>
    public string PickerScriptFor(
        ReDocDocumentPage page,
        ReDocToolbarSpot spot = ReDocToolbarSpot.Shared,
        bool searchable = false) =>
        ReDocDocumentPicker.BuildScript(PickerPagesFrom(page), links, page, spot, searchable);

    private static string RouteOf(ReDocDocument doc, string prefix)
    {
        if (doc.Route is not null)
        {
            return doc.Route.Trim('/');
        }

        return prefix.Length == 0 ? doc.Name : $"{prefix}/{doc.Name}";
    }

    private static void UseRedirect(IApplicationBuilder app, ReDocPrefixRedirect redirect)
    {
        app.Use((context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method) && redirect.Matches(context.Request.Path))
            {
                context.Response.Redirect($"{context.Request.PathBase}{redirect.To}");
                return Task.CompletedTask;
            }

            return next(context);
        });
    }

    private static void Validate(IReadOnlyList<ReDocDocument> docs, IReadOnlyList<ReDocLink> links)
    {
        if (docs.Count == 0)
        {
            throw new ArgumentException("At least one document is required.", "documents");
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReDocDocument doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Name) || doc.Name.Contains('/'))
            {
                throw new ArgumentException(
                    $"Document name '{doc.Name}' must be non-empty and contain no '/'.", "documents");
            }

            if (!seen.Add(doc.Name))
            {
                throw new ArgumentException($"Duplicate document name '{doc.Name}'.", "documents");
            }
        }

        foreach (ReDocLink link in links)
        {
            if (string.IsNullOrWhiteSpace(link.Label) || string.IsNullOrWhiteSpace(link.Url))
            {
                throw new ArgumentException(
                    "Every picker link needs a non-empty label and URL.", "configure");
            }
        }
    }

    /// <summary>
    /// Fails fast when a document name doesn't match any registered OpenAPI document. The page
    /// would otherwise mount fine and the app start clean, because only the browser's spec
    /// request 404s — so the mistake would surface in ReDoc's error screen and nowhere else.
    /// </summary>
    private void ValidateDocumentNames(IServiceProvider services, string caller)
    {
        // Without the placeholder, names don't drive spec URLs, so there is nothing to check.
        if (!options.ValidateDocumentNames
            || !options.SpecUrlTemplate.Contains("{documentName}", StringComparison.Ordinal))
        {
            return;
        }

        // A document with an explicit SpecUrl says where its spec is, whoever serves it.
        ReDocDocumentPage[] checkable =
            [.. Pages.Where(page => page.Document.SpecUrl is null)];
        if (checkable.Length == 0)
        {
            return;
        }

        HashSet<string>? registered = OpenApiDocumentNames.TryResolve(services);
        if (registered is null)
        {
            return;
        }

        ReDocDocumentPage[] missing =
            [.. checkable.Where(page => !registered.Contains(page.Document.Name))];
        if (missing.Length == 0)
        {
            return;
        }

        IEnumerable<string> details = missing.Select(page =>
            $"  - '{page.Document.Name}': page '/{page.Route}/' would load an empty spec from " +
            $"'{SpecUrlOf(page.Document)}'.");
        string names = string.Join(", ", registered.Order().Select(name => $"'{name}'"));
        string known = registered.Count == 0
            ? "No OpenAPI documents are registered."
            : $"Registered documents: {names}.";

        throw new InvalidOperationException(
            $"{caller} was given OpenAPI document names that aren't registered:\n"
            + string.Join('\n', details) + '\n' + known + '\n'
            + "Fix the name, register the document with AddOpenApi(name) or Swashbuckle's "
            + "SwaggerDoc(name, ...), set ReDocDocument.SpecUrl if its spec is served from "
            + "somewhere else, or set ReDocDocumentsOptions.ValidateDocumentNames = false to skip "
            + "this check.");
    }

    private static void ValidateRoutes(IReadOnlyList<ReDocDocumentPage> pages)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReDocDocumentPage page in pages)
        {
            if (page.Route.Contains("//", StringComparison.Ordinal)
                || page.Route.IndexOfAny(['?', '#', '\\']) >= 0)
            {
                throw new ArgumentException(
                    $"Route '{page.Route}' of document '{page.Document.Name}' must be a plain " +
                    "path.",
                    "documents");
            }

            if (!seen.Add(page.Route))
            {
                throw new ArgumentException(
                    $"Documents share the route '{page.Route}'; every page needs its own.",
                    "documents");
            }
        }
    }
}
