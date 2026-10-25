namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Builds hrefs between the pages this package serves, relative to the page they appear on.
/// </summary>
/// <remarks>
/// Every page sits at <c>{base}/{route}/</c>, so a link one page writes to another is
/// <c>../</c> per segment of its own route followed by the target's route. Such an href needs no
/// knowledge of the app's <c>PathBase</c> and keeps working behind a path-rewriting proxy, which
/// an absolute path would not.
/// </remarks>
internal static class ReDocRelativePath
{
    /// <summary>
    /// The href from the page at <paramref name="fromRoute"/> to <paramref name="toRoute"/>, which
    /// may be a route (no trailing slash is added) or a file path. Both are relative to the app
    /// base, without slashes at either end.
    /// </summary>
    public static string Between(string fromRoute, string toRoute)
    {
        string href = ToBase(fromRoute) + toRoute;
        return href.Length == 0 ? "./" : href;
    }

    /// <summary>The <c>../</c> prefix that walks from a page's own route back to the app base.</summary>
    public static string ToBase(string route) =>
        string.Concat(Enumerable.Repeat("../", SegmentCount(route)));

    /// <summary>Percent-encodes each segment of a route, leaving the separators alone.</summary>
    public static string EscapeRoute(string route) =>
        string.Join('/', route.Split('/').Select(Uri.EscapeDataString));

    private static int SegmentCount(string route) =>
        route.Length == 0 ? 0 : route.Split('/').Length;
}
