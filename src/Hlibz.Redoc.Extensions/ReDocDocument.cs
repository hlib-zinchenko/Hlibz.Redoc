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
/// <param name="Group">
/// Heading this document sits under in the picker, as an <c>&lt;optgroup&gt;</c> — e.g. <c>"v2"</c>
/// so the versions of one API sit together. Defaults to <see langword="null"/>, which puts it
/// directly in the list.
/// <para>
/// Documents keep the order you give them, and a group appears where its first member does, so
/// grouped and ungrouped documents can be mixed. The heading is a label only: it isn't selectable
/// and it changes no route.
/// </para>
/// </param>
/// <param name="Unlisted">
/// Keeps this document out of the picker on <em>listed</em> documents' pages. Unlisted documents
/// still list each other, and still list the rest, so someone who reaches one can navigate
/// normally. Defaults to <see langword="false"/>.
/// <para>
/// This is the companion to per-document authorization: it hides the link, so a public page's
/// dropdown doesn't advertise pages its viewer can't open. <strong>It protects nothing.</strong>
/// The page is served to anyone who knows its URL unless something else stops them — on
/// <c>Hlibz.Redoc.AspNetCore</c>, <c>MapReDocDocuments(...).ForDocument(name)</c> and a
/// <c>RequireAuthorization</c> on it.
/// </para>
/// </param>
public sealed record ReDocDocument(
    string Name,
    string? DisplayName = null,
    string? SpecUrl = null,
    string? Route = null,
    bool Unlisted = false,
    string? Group = null);
