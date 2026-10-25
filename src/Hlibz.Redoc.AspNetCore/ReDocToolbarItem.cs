namespace Hlibz.Redoc.Extensions;

/// <summary>
/// A link shown as a button in the toolbar at the top right of every page, beside the document
/// picker. Added via <see cref="ReDocDocumentsOptions.AddToolbarItem"/>.
/// </summary>
/// <param name="Label">Text on the button.</param>
/// <param name="Url">
/// Where it goes. An absolute URL (<c>https://...</c>) or a root-relative path (<c>/postman</c>)
/// is used as-is; a path starting with <c>~/</c> (<c>~/openapi/widgets.json</c>) is resolved
/// against the app's base path, so it keeps working under a <c>PathBase</c> or a reverse proxy.
/// Same rule as <see cref="ReDocLink"/>.
/// </param>
/// <param name="NewTab">
/// Whether to open in a new tab (<c>target="_blank"</c>, with <c>rel="noopener"</c>). Defaults to
/// <see langword="false"/>. Worth setting for somewhere the viewer isn't coming back from, like a
/// download or another site.
/// </param>
/// <remarks>
/// <para>
/// This is for the links a documentation site usually wants and this package shouldn't ship one by
/// one: a Postman collection, a changelog, the dev portal, a "Try it" console rendered by
/// something else. For a link to <em>another ReDoc page of your own</em>, prefer
/// <see cref="ReDocDocumentsOptionsBase.Links"/>, which lists it inside the picker where a reader
/// already looks for documents.
/// </para>
/// <para>
/// A toolbar item is a link and nothing else — there is deliberately no raw HTML, CSS or script
/// escape hatch here. Markup or a script supplied per page would have to be inlined into it, and
/// a page with anything inline needs <c>'unsafe-inline'</c> or a nonce in <c>script-src</c>, which
/// is the one property this host exists to keep. <see cref="ReDocDocumentsOptions.AdditionalCss"/>
/// is served as a file and can restyle these buttons as far as CSS reaches.
/// </para>
/// </remarks>
public sealed record ReDocToolbarItem(string Label, string Url, bool NewTab = false);
