extern alias redocaspnetcore;

using Microsoft.AspNetCore.Builder;

using redocaspnetcore::Hlibz.Redoc.Extensions;

namespace Hlibz.Redoc.Extensions.ThemeTests;

/// <summary>
/// The pages on Redoc.AspNetCore (redoc 2.0.0). Line-for-line <see cref="SwashbuckleSite"/> but
/// for the alias: the code a consumer writes is the same on both hosts, which is what lets the
/// rendered pages be compared at all.
/// </summary>
internal static class RedocAspNetCoreSite
{
    public static void Map(IApplicationBuilder app) =>
        app.UseReDocDocuments(
            [
                new ReDocDocument("widgets", "Widget Catalog"),
                new ReDocDocument("manufacturers", "Manufacturers"),
            ],
            docs => docs.ConfigureReDoc = (redoc, document) => redoc.UseDarkTheme(theme =>
            {
                // One page with the selector, starting light, for the re-init path; the other
                // is the plain dark theme most consumers install.
                if (document.Name == ThemeSites.SelectorDocument)
                {
                    theme.EnableThemeSelector(defaultTheme: ReDocTheme.Light);
                }
            }));
}
