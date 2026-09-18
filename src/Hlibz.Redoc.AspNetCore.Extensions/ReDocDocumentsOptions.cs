using Redoc.AspNetCore;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Options for <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>.
/// </summary>
public sealed class ReDocDocumentsOptions
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
    /// Plain links listed in the picker after the documents, e.g. to Swagger UI or another
    /// service's docs.
    /// </summary>
    public IList<ReDocLink> Links { get; } = [];

    /// <summary>
    /// Extra configuration applied to every document's <see cref="ReDocOptions"/>, e.g.
    /// <c>(redoc, _) =&gt; redoc.UseDarkTheme()</c>. Runs after the route, spec URL and title are
    /// set. It must not change <see cref="ReDocOptions.RoutePrefix"/>; set
    /// <see cref="ReDocDocument.Route"/> instead, so the picker's links know every page's route.
    /// </summary>
    public Action<ReDocOptions, ReDocDocument>? ConfigureReDoc { get; set; }
}
