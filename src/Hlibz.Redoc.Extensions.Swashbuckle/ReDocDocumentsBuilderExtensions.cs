using Microsoft.AspNetCore.Builder;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Extension methods for mounting several OpenAPI documents as ReDoc pages with a picker.
/// </summary>
public static class ReDocDocumentsBuilderExtensions
{
    /// <summary>
    /// Mounts one ReDoc page per document, by default at <c>/{RoutePrefix}/{Name}/</c>, each with
    /// a picker for switching between them, and redirects <c>/{RoutePrefix}</c> to the first one.
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

        ReDocDocumentsPlan plan =
            ReDocDocumentsPlan.Build(app, documents, options, nameof(UseReDocDocuments));

        foreach (ReDocDocumentPage page in plan.Pages)
        {
            // This host resolves a fresh ReDocOptions per call, from a scope of its own, so every
            // page gets its own instance — and the setup-action overload, rather than
            // UseReDoc(options), keeps a caller's services.Configure<ReDocOptions> defaults
            // in play.
            app.UseReDoc(redoc =>
            {
                redoc.RoutePrefix = page.Route;
                redoc.SpecUrl = plan.SpecUrlOf(page.Document);
                redoc.DocumentTitle = plan.DocumentTitleOf(page.Document);
                options.ConfigureReDoc?.Invoke(redoc, page.Document);
                if (!string.Equals(redoc.RoutePrefix, page.Route, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"ConfigureReDoc changed RoutePrefix for document '{page.Document.Name}'. " +
                        "Set ReDocDocument.Route instead, so the picker can link to the page.");
                }

                redoc.HeadContent = $"{redoc.HeadContent}\n{plan.PickerFor(page)}";
            });
        }

        return app;
    }
}
