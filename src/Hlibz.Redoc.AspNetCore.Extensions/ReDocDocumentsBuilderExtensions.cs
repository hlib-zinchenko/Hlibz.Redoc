using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Redoc.AspNetCore;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Extension methods for mounting several OpenAPI documents as ReDoc pages with a picker.
/// </summary>
public static class ReDocDocumentsBuilderExtensions
{
    /// <summary>
    /// Mounts one ReDoc page per document, by default at <c>/{RoutePrefix}/{Name}/</c>, each with a
    /// picker for switching between them, and redirects <c>/{RoutePrefix}</c> to the first document.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="documents">The documents to mount, in picker order.</param>
    /// <param name="configure">
    /// Optional route, redirect, spec URL, extra links, and per-page ReDoc configuration.
    /// </param>
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

        string prefix = options.RoutePrefix.Trim('/');
        IReadOnlyList<ReDocDocument> docs = [.. documents];
        IReadOnlyList<ReDocLink> links = [.. options.Links];
        Validate(docs, links);

        // UseReDoc runs its setup action right away, so every page's route has to be known before
        // the first one is built: each picker links to all of them.
        IReadOnlyList<ReDocDocumentPage> pages =
            [.. docs.Select(doc => new ReDocDocumentPage(doc, RouteOf(doc, prefix)))];
        ValidateRoutes(pages);

        if (options.RedirectToFirstDocument
            && !pages.Any(page => string.Equals(page.Route, prefix, StringComparison.OrdinalIgnoreCase)))
        {
            UseRedirect(app, prefix, pages[0].Route);
        }

        foreach (ReDocDocumentPage page in pages)
        {
            app.UseReDoc(redoc =>
            {
                redoc.RoutePrefix = page.Route;
                redoc.SpecUrl(page.Document.SpecUrl
                    ?? options.SpecUrlTemplate.Replace("{documentName}", page.Document.Name));
                // Redoc.AspNetCore writes the title into <title> as-is.
                redoc.DocumentTitle =
                    WebUtility.HtmlEncode(page.Document.DisplayName ?? page.Document.Name);
                options.ConfigureReDoc?.Invoke(redoc, page.Document);
                if (!string.Equals(redoc.RoutePrefix, page.Route, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"ConfigureReDoc changed RoutePrefix for document '{page.Document.Name}'. " +
                        "Set ReDocDocument.Route instead, so the picker can link to the page.");
                }

                redoc.HeadContent = $"{redoc.HeadContent}\n{ReDocDocumentPicker.Build(pages, links, page)}";
            });
        }

        return app;
    }

    private static string RouteOf(ReDocDocument doc, string prefix)
    {
        if (doc.Route is not null)
        {
            return doc.Route.Trim('/');
        }

        return prefix.Length == 0 ? doc.Name : $"{prefix}/{doc.Name}";
    }

    private static void UseRedirect(IApplicationBuilder app, string prefix, string firstRoute)
    {
        string root = prefix.Length == 0 ? string.Empty : $"/{prefix}";
        string target = firstRoute.Length == 0 ? "/" : $"/{firstRoute}/";

        app.Use((context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (HttpMethods.IsGet(context.Request.Method)
                && (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, $"{root}/", StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.Redirect($"{context.Request.PathBase}{target}");
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

    private static void ValidateRoutes(IReadOnlyList<ReDocDocumentPage> pages)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReDocDocumentPage page in pages)
        {
            if (page.Route.Contains("//", StringComparison.Ordinal)
                || page.Route.IndexOfAny(['?', '#', '\\']) >= 0)
            {
                throw new ArgumentException(
                    $"Route '{page.Route}' of document '{page.Document.Name}' must be a plain path.",
                    "documents");
            }

            if (!seen.Add(page.Route))
            {
                throw new ArgumentException(
                    $"Documents share the route '{page.Route}'; every page needs its own.", "documents");
            }
        }
    }
}
