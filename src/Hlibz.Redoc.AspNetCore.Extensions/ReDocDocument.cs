namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// An OpenAPI document mounted as its own ReDoc page by
/// <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>.
/// </summary>
/// <param name="Name">
/// The OpenAPI document name, e.g. <c>"taxes"</c> for <c>AddOpenApi("taxes")</c>. Used as the page's
/// route segment and to fill <see cref="ReDocDocumentsOptions.SpecUrlTemplate"/>.
/// </param>
/// <param name="DisplayName">
/// Label shown in the document picker and the browser tab. Defaults to <paramref name="Name"/>.
/// </param>
/// <param name="SpecUrl">
/// Explicit spec URL for this document, overriding <see cref="ReDocDocumentsOptions.SpecUrlTemplate"/>.
/// </param>
public sealed record ReDocDocument(string Name, string? DisplayName = null, string? SpecUrl = null);
