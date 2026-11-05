namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Where the try-it console keeps the credentials a viewer types, set through
/// <see cref="ReDocTryItOptions.CredentialStorage"/>.
/// </summary>
/// <remarks>
/// Whatever is stored is stored once for the whole app, not per page: a token typed on one
/// document is there on every other document the picker leads to, keyed by security scheme name.
/// Anything a script on this origin can run can read browser storage, so a stored secret is only
/// as safe as the origin it lives on.
/// </remarks>
public enum ReDocTryItCredentialStorage
{
    /// <summary>
    /// Kept in the page's memory only: gone on a reload, and on a switch to another document.
    /// The default.
    /// </summary>
    Memory,

    /// <summary>
    /// Kept in <c>sessionStorage</c>: survives reloads and switches between documents in the same
    /// tab, and is gone when the tab is closed.
    /// </summary>
    Session,

    /// <summary>
    /// Kept in <c>localStorage</c>: survives reloads, document switches, new tabs and browser
    /// restarts, until the viewer clears it with the console's "Forget" button or their browser's
    /// site data. Only for docs whose viewers expect a browser to remember their keys.
    /// </summary>
    Local,
}
