namespace Hlibz.Redoc.Extensions;

/// <summary>
/// A plain link listed in the document picker after the documents, e.g. to Swagger UI or another
/// service's docs. Added via <see cref="ReDocDocumentsOptionsBase.Links"/>.
/// </summary>
/// <param name="Label">Text shown in the picker.</param>
/// <param name="Url">
/// Where the link goes. An absolute URL (<c>https://...</c>) or a root-relative path
/// (<c>/swagger</c>) is used as-is; a path starting with <c>~/</c> (<c>~/swagger</c>) is resolved
/// against the app's base path, so it keeps working under a <c>PathBase</c> or reverse proxy.
/// </param>
public sealed record ReDocLink(string Label, string Url);
