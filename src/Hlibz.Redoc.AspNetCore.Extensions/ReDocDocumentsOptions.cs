using Redoc.AspNetCore;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Options for <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>.
/// </summary>
public sealed class ReDocDocumentsOptions
{
    /// <summary>
    /// Route prefix the document pages are mounted under: each document is served at
    /// <c>/{RoutePrefix}/{Name}/</c>, and <c>/{RoutePrefix}</c> redirects to the first one.
    /// Defaults to <c>"api-docs"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "api-docs";

    /// <summary>
    /// Spec URL for each document, with <c>{documentName}</c> replaced by
    /// <see cref="ReDocDocument.Name"/>. Defaults to <c>"/openapi/{documentName}.json"</c>, which
    /// matches <c>MapOpenApi()</c>.
    /// </summary>
    public string SpecUrlTemplate { get; set; } = "/openapi/{documentName}.json";

    /// <summary>
    /// Extra configuration applied to every document's <see cref="ReDocOptions"/>, e.g.
    /// <c>(redoc, _) =&gt; redoc.UseDarkTheme()</c>. Runs after the route, spec URL and title are
    /// set.
    /// </summary>
    public Action<ReDocOptions, ReDocDocument>? ConfigureReDoc { get; set; }
}
