using Microsoft.AspNetCore.Http;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The redirect from the route prefix to the first document's page, as data.
/// </summary>
/// <param name="From">
/// The prefix path, without a trailing slash — <c>"/api-docs"</c>, or empty when the prefix is the
/// app root. <see cref="Matches"/> also accepts it with a trailing slash.
/// </param>
/// <param name="To">The first page's path, with a trailing slash, relative to the app base.</param>
internal sealed record ReDocPrefixRedirect(string From, string To)
{
    /// <summary>Whether a request path is the prefix, with or without its trailing slash.</summary>
    public bool Matches(PathString path)
    {
        string value = path.Value ?? string.Empty;
        return string.Equals(value, From, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, $"{From}/", StringComparison.OrdinalIgnoreCase);
    }
}
