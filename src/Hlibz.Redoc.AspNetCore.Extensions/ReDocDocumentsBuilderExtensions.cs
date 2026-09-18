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
    /// Mounts one ReDoc page per document at <c>/{RoutePrefix}/{Name}/</c>, each with a picker
    /// for switching between them, and redirects <c>/{RoutePrefix}</c> to the first document.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="documents">The documents to mount, in picker order.</param>
    /// <param name="configure">Optional route, spec URL, and per-page ReDoc configuration.</param>
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

        IReadOnlyList<ReDocDocument> docs = [.. documents];
        Validate(docs);

        string prefix = options.RoutePrefix.Trim('/');
        string root = prefix.Length == 0 ? string.Empty : $"/{prefix}";
        string firstPage = $"{root}/{docs[0].Name}/";

        app.Use((context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (HttpMethods.IsGet(context.Request.Method) && (path == root || path == $"{root}/"))
            {
                context.Response.Redirect($"{context.Request.PathBase}{firstPage}");
                return Task.CompletedTask;
            }

            return next(context);
        });

        foreach (ReDocDocument doc in docs)
        {
            app.UseReDoc(redoc =>
            {
                redoc.RoutePrefix = prefix.Length == 0 ? doc.Name : $"{prefix}/{doc.Name}";
                redoc.SpecUrl(
                    doc.SpecUrl ?? options.SpecUrlTemplate.Replace("{documentName}", doc.Name));
                redoc.DocumentTitle = doc.DisplayName ?? doc.Name;
                options.ConfigureReDoc?.Invoke(redoc, doc);
                redoc.HeadContent = $"{redoc.HeadContent}\n{ReDocDocumentPicker.Build(docs, doc)}";
            });
        }

        return app;
    }

    private static void Validate(IReadOnlyList<ReDocDocument> docs)
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
    }
}
