using Redoc.AspNetCore;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Extension methods for applying a dark theme to <see cref="ReDocOptions"/>.
/// </summary>
public static class RedocOptionsExtensions
{
    /// <summary>
    /// Applies a dark theme to this <see cref="ReDocOptions"/> instance, fixing several colors
    /// ReDoc hardcodes outside its own theme config (scrollbar, selection, section captions,
    /// nested-schema backgrounds, sample-tab text) along with a full dark color palette, and adds
    /// a print stylesheet so the page doesn't print as a black rectangle. Call inside
    /// <c>app.UseReDoc(options => ...)</c>.
    /// </summary>
    /// <remarks>
    /// With <see cref="DarkThemeOptions.LoadGoogleFonts"/> turned off this also replaces
    /// <see cref="ReDocOptions.IndexStream"/> with a wrapper around the current one, so set your
    /// own template before calling this.
    /// </remarks>
    /// <param name="options">The options to apply the dark theme to.</param>
    /// <param name="configure">Optional customization, e.g. overriding the accent color.</param>
    /// <returns>The same <paramref name="options"/> instance, for chaining.</returns>
    public static ReDocOptions UseDarkTheme(this ReDocOptions options, Action<DarkThemeOptions>? configure = null)
    {
        DarkThemeOptions theme = new();
        configure?.Invoke(theme);

        options.ConfigObject.AdditionalItems["theme"] = ReDocDarkThemeDefaults.BuildConfigValue(theme);
        options.HeadContent = ReDocDarkThemeDefaults.BuildHeadContent(theme);

        // The font <link> sits above %(HeadContent) in the index template, so it can only be
        // dropped by rewriting the page. Wrapping whatever IndexStream is in effect leaves a
        // caller's own template in charge of everything else.
        if (!theme.LoadGoogleFonts && options.IndexStream is not null)
        {
            options.IndexStream = ReDocFonts.WithoutWebFontLinks(options.IndexStream);
        }

        return options;
    }
}
