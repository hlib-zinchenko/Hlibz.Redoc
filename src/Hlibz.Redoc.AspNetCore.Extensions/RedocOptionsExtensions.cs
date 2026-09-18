using Redoc.AspNetCore;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Extension methods for applying a dark theme to <see cref="ReDocOptions"/>.
/// </summary>
public static class RedocOptionsExtensions
{
    /// <summary>
    /// Applies a dark theme to this <see cref="ReDocOptions"/> instance, fixing several colors
    /// ReDoc hardcodes outside its own theme config (scrollbar, selection, section captions,
    /// nested-schema backgrounds, sample-tab text) along with a full dark color palette. Call
    /// inside <c>app.UseReDoc(options => ...)</c>.
    /// </summary>
    /// <param name="options">The options to apply the dark theme to.</param>
    /// <param name="configure">Optional customization, e.g. overriding the accent color.</param>
    /// <returns>The same <paramref name="options"/> instance, for chaining.</returns>
    public static ReDocOptions UseDarkTheme(this ReDocOptions options, Action<DarkThemeOptions>? configure = null)
    {
        DarkThemeOptions theme = new();
        configure?.Invoke(theme);

        options.ConfigObject.AdditionalItems["theme"] = ReDocDarkThemeDefaults.BuildConfigValue(theme);
        options.HeadContent = ReDocDarkThemeDefaults.BuildHeadContent(theme);
        return options;
    }
}
