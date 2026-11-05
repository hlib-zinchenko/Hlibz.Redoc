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
    /// Where credentials a viewer types are kept. Defaults to
    /// <see cref="ReDocTryItCredentialStorage.Memory"/>: they are gone on a reload or a switch to
    /// another document. <see cref="ReDocTryItCredentialStorage.Session"/> keeps them for the tab,
    /// <see cref="ReDocTryItCredentialStorage.Local"/> in the browser until cleared. Either way
    /// they are stored once for the whole app and shared by every document's page.
    /// </summary>
    /// <remarks>
    /// Storing them turns on <see cref="ReDocDocumentsOptions.DefaultContentSecurityPolicy"/> for
    /// the pages unless <see cref="ReDocDocumentsOptions.ContentSecurityPolicy"/> is set or your
    /// own middleware sends a policy: an injected script is what would read a stored secret, and
    /// <c>script-src 'self'</c> is what keeps one off the page.
    /// </remarks>
    public ReDocTryItCredentialStorage CredentialStorage { get; set; }

    /// <summary>
    /// How long stored credentials are kept after they were last typed, or
    /// <see langword="null"/> to keep them until the viewer presses "Forget" or clears site data.
    /// Defaults to 8 hours. Only matters when <see cref="CredentialStorage"/> stores them; checked
    /// whenever the console opens.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public TimeSpan? CredentialLifetime
    {
        get => credentialLifetime;
        set
        {
            if (value is { } lifetime && lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), lifetime,
                    "CredentialLifetime must be positive, or null to keep credentials until "
                    + "they are forgotten.");
            }

            credentialLifetime = value;
        }
    }

    private TimeSpan? credentialLifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// Whether requests carry the viewer's cookies for this origin. Defaults to
    /// <see langword="true"/>, so an API using cookie authentication works for a viewer who is
    /// already signed in. Set it to <see langword="false"/> to send only what the viewer types into
    /// the console.
    /// </summary>
    public bool SendCookies { get; set; } = true;
}
