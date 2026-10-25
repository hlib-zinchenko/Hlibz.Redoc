namespace Hlibz.Redoc.Extensions;

/// <summary>
/// A document together with the route its page is served at, without slashes at either end.
/// </summary>
internal sealed record ReDocDocumentPage(ReDocDocument Document, string Route);
