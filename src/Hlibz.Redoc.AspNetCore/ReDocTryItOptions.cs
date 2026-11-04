namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Settings for the try-it console, turned on with
/// <see cref="ReDocDocumentsOptions.EnableTryIt"/>.
/// </summary>
/// <remarks>
/// <para>
/// The console adds a "Try it" button to every operation. It opens a panel where a viewer fills
/// in parameters, a JSON body and credentials, sends the request from their browser, and reads the
/// response. Bearer tokens, HTTP Basic and API keys (header or query) are supported. OAuth 2.0 and
/// OpenID Connect schemes accept a pasted access token. Other media types are sent as typed.
/// </para>
/// <para>
/// Requests go from the viewer's browser, so a server on another origin must allow it through
/// CORS, and the page's <c>connect-src</c> must list it.
/// <see cref="ReDocDocumentsOptions.DefaultContentSecurityPolicy"/> allows this origin only. An
/// operation can opt out with <c>x-hideTryItPanel: true</c>, the extension Redocly's own console
/// honours.
/// </para>
/// </remarks>
public sealed class ReDocTryItOptions
{
    /// <summary>
    /// Whether credentials a viewer types survive a reload of the page, in
    /// <c>sessionStorage</c>. Defaults to <see langword="false"/>: they are kept in memory only
    /// and are gone when the page is closed or reloaded. Never written to <c>localStorage</c>.
    /// </summary>
    public bool PersistCredentials { get; set; }

    /// <summary>
    /// Whether requests carry the viewer's cookies for this origin. Defaults to
    /// <see langword="true"/>, so an API using cookie authentication works for a viewer who is
    /// already signed in. Set it to <see langword="false"/> to send only what the viewer types into
    /// the console.
    /// </summary>
    public bool SendCookies { get; set; } = true;
}
