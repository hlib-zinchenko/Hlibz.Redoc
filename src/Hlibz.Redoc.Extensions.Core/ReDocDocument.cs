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
/// <strong>It protects nothing.</strong> The page is served to anyone who knows its URL unless
/// something else stops them — a policy, or your own middleware. What it is for is a document you
/// still serve but would rather not advertise: a superseded version that existing links should
/// keep reaching, or one whose pages something outside this package already gates.
/// </para>
/// <para>
/// On <c>Hlibz.Redoc.AspNetCore</c>, don't reach for this to hide an internal document:
/// <c>RequireAuthorization</c> on the document already implies it, and also protects the page and
/// its spec. Setting it by hand there gets you the hiding without the protection.
/// </para>
/// </param>
/// <remarks>
/// Not sealed, so <c>Hlibz.Redoc.AspNetCore</c> can carry per-document authorization on a record
/// of its own without that concept reaching the hosts that cannot enforce it. Nothing in this
/// package may copy, rebuild or compare a document as a result — a <c>with</c> expression or a
/// reconstruction from parts would silently drop whatever a derived record adds.
/// </remarks>
public record ReDocDocument(
    string Name,
    string? DisplayName = null,
    string? SpecUrl = null,
    string? Route = null,
    bool Unlisted = false,
    string? Group = null);
