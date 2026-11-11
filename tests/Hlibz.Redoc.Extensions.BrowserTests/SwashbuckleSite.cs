extern alias swashbuckle;

using Microsoft.AspNetCore.Builder;

using swashbuckle::Hlibz.Redoc.Extensions;

namespace Hlibz.Redoc.Extensions.ThemeTests;

/// <summary>
/// The pages on Swashbuckle.AspNetCore.ReDoc (redoc 2.5.4). Line-for-line
/// <see cref="RedocAspNetCoreSite"/> but for the alias.
/// </summary>
internal static class SwashbuckleSite
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
