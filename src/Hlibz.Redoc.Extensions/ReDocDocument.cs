namespace Hlibz.Redoc.Extensions;

/// <summary>
/// An OpenAPI document mounted as its own ReDoc page by
/// <c>UseReDocDocuments</c>.
/// </summary>
/// <param name="Name">
/// The OpenAPI document name, e.g. <c>"taxes"</c> for <c>AddOpenApi("taxes")</c>. Used to fill
/// <see cref="ReDocDocumentsOptionsBase.SpecUrlTemplate"/> and, unless <paramref name="Route"/> is set,
/// as the page's route segment. Must not contain <c>/</c>.
/// </param>
/// <param name="DisplayName">
/// Label shown in the document picker and the browser tab. Defaults to <paramref name="Name"/>.
/// </param>
/// <param name="SpecUrl">
/// Explicit spec URL for this document, overriding
/// <see cref="ReDocDocumentsOptionsBase.SpecUrlTemplate"/>.
/// </param>
/// <param name="Route">
/// Path the page is served at, relative to the app's base path, e.g. <c>"docs/v2/taxes"</c>. May
/// contain <c>/</c>; leading and trailing slashes are ignored, and an empty route serves the page
/// at the app root. Defaults to <c>{RoutePrefix}/{Name}</c>.
/// </param>
public sealed record ReDocDocument(
    string Name,
    string? DisplayName = null,
    string? SpecUrl = null,
    string? Route = null);
