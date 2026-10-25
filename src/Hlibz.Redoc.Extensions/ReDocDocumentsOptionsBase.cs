namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The host-independent options for mounting several OpenAPI documents as ReDoc pages with a
/// picker. Each host adapter derives a <c>ReDocDocumentsOptions</c> from this and adds its own
/// <c>ConfigureReDoc</c>, typed against that host's ReDoc options.
/// </summary>
public abstract class ReDocDocumentsOptionsBase
{
    /// <summary>
    /// Route prefix the document pages are mounted under: each document without its own
    /// <see cref="ReDocDocument.Route"/> is served at <c>/{RoutePrefix}/{Name}/</c>. Defaults to
    /// <c>"api-docs"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "api-docs";

    /// <summary>
    /// Whether a GET to <c>/{RoutePrefix}</c> redirects to the first document's page. Defaults to
    /// <see langword="true"/>. Turn it off when something else is served at that path. No redirect
    /// is added when a document's <see cref="ReDocDocument.Route"/> is the prefix itself.
    /// </summary>
    public bool RedirectToFirstDocument { get; set; } = true;

    /// <summary>
    /// Spec URL for each document, with <c>{documentName}</c> replaced by
    /// <see cref="ReDocDocument.Name"/>. Defaults to <c>"/openapi/{documentName}.json"</c>, which
    /// matches <c>MapOpenApi()</c>.
    /// </summary>
    public string SpecUrlTemplate { get; set; } = "/openapi/{documentName}.json";

    /// <summary>
    /// Whether to check at startup that every <see cref="ReDocDocument.Name"/> matches a
    /// registered OpenAPI document, and throw instead of serving a page whose spec URL 404s.
    /// Defaults to <see langword="true"/>. Both <c>AddOpenApi(name)</c> and Swashbuckle's
    /// <c>SwaggerDoc(name, ...)</c> are recognized, case-insensitively.
    /// </summary>
    /// <remarks>
    /// A document is only checked when its name actually drives its spec URL: documents with an
    /// explicit <see cref="ReDocDocument.SpecUrl"/> are skipped, as are all documents when
    /// <see cref="SpecUrlTemplate"/> has no <c>{documentName}</c> placeholder, and nothing is
    /// checked when the app registers no recognized OpenAPI document provider. Turn this off when
    /// specs come from somewhere the check can't see, e.g. static files served next to an
    /// <c>AddOpenApi</c> document.
    /// </remarks>
    public bool ValidateDocumentNames { get; set; } = true;

    /// <summary>
    /// Plain links listed in the picker after the documents, e.g. to Swagger UI or another
    /// service's docs.
    /// </summary>
    public IList<ReDocLink> Links { get; } = [];
}
