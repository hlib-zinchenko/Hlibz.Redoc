using Microsoft.AspNetCore.Http;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Remembers which document a viewer was last reading, so the route prefix can send them back to
/// it rather than to the first one.
/// </summary>
/// <remarks>
/// A cookie rather than <c>localStorage</c>, because the decision is the server's: the prefix
/// answers with a redirect before any script of ours has run. It carries a document name and
/// nothing else, it is never read by script, and the redirect only honours a name that is still a
/// listed document of this site — so a stale or edited cookie falls back to the first page instead
/// of going anywhere unexpected.
/// </remarks>
internal sealed class ReDocLastDocument
{
    /// <summary>The cookie's name, namespaced so it can't collide with ReDoc's own.</summary>
    public const string CookieName = "hlibz.redoc.document";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    private readonly Dictionary<string, string> routesByName;
    private readonly string prefixPath;

    /// <param name="pages">The site's pages. Unlisted ones are deliberately not remembered.</param>
    /// <param name="prefixPath">
    /// The route prefix with a leading slash and no trailing one, or empty at the app root: the
    /// path the cookie is scoped to, so it is sent back with a request for the prefix and with
    /// nothing else in the app.
    /// </param>
    public ReDocLastDocument(IEnumerable<ReDocSitePage> pages, string prefixPath)
    {
        routesByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ReDocSitePage page in pages)
        {
            if (!page.Unlisted)
            {
                routesByName[page.Name] = page.Route;
            }
        }

        this.prefixPath = prefixPath;
    }

    /// <summary>Records the document this response is a page for.</summary>
    public void Remember(HttpContext context, string documentName)
    {
        if (!routesByName.ContainsKey(documentName))
        {
            return;
        }

        context.Response.Cookies.Append(CookieName, documentName, new CookieOptions
        {
            // Never read by script, so nothing is gained by exposing it to one.
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = PathFor(context),
            MaxAge = Lifetime,
        });
    }

    /// <summary>
    /// Where the prefix should send this request: the remembered document's page, or
    /// <see langword="null"/> to use the site's own first-document redirect.
    /// </summary>
    public string? RouteFor(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out string? name)
        && name is not null
        && routesByName.TryGetValue(name, out string? route)
            ? route
            : null;

    /// <summary>
    /// The cookie's path, including whatever <c>PathBase</c> the app is mounted under, so it comes
    /// back with a request for the prefix and is sent with nothing else.
    /// </summary>
    private string PathFor(HttpContext context)
    {
        string path = context.Request.PathBase.Value ?? string.Empty;
        return $"{path}{prefixPath}" is { Length: > 0 } scoped ? scoped : "/";
    }
}
