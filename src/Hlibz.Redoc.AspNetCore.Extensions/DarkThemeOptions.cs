namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Customization options for <see cref="RedocOptionsExtensions.UseDarkTheme"/>.
/// </summary>
public sealed class DarkThemeOptions
{
    internal const string DefaultPrimaryColor = "#38bdf8";

    /// <summary>
    /// Accent color used for primary UI elements, links, active sidebar items, and the "GET"
    /// HTTP method badge. Defaults to a sky blue (<c>#38bdf8</c>).
    /// </summary>
    public string PrimaryColor { get; set; } = DefaultPrimaryColor;

    /// <summary>
    /// Lighter shade of <see cref="PrimaryColor"/>, used for link hover/visited states. When not
    /// set, a shade is computed automatically from <see cref="PrimaryColor"/>.
    /// </summary>
    public string? PrimaryColorLight { get; set; }

    /// <summary>
    /// Darker shade of <see cref="PrimaryColor"/>. When not set, a shade is computed
    /// automatically from <see cref="PrimaryColor"/>.
    /// </summary>
    public string? PrimaryColorDark { get; set; }

    /// <summary>
    /// Additional raw CSS injected into the generated <c>&lt;style&gt;</c> block, appended after
    /// the theme's built-in overrides.
    /// </summary>
    public string? AdditionalCss { get; set; }

    internal bool ThemeSelectorEnabled { get; private set; }

    internal ReDocTheme DefaultTheme { get; private set; } = ReDocTheme.Dark;

    /// <summary>
    /// Shows a Light/System/Dark theme selector in the ReDoc UI, letting each viewer switch between
    /// this package's dark theme, ReDoc's stock light theme, or following their OS setting. The
    /// viewer's choice is remembered in the browser's <c>localStorage</c>;
    /// <paramref name="defaultTheme"/> is used until they make one.
    /// </summary>
    /// <remarks>
    /// With the selector enabled, the dark overrides are scoped under
    /// <c>html[data-redoc-theme="dark"]</c>. <see cref="AdditionalCss"/> stays unscoped and applies
    /// to both themes — prefix rules with that selector to make them dark-only.
    /// </remarks>
    /// <param name="defaultTheme">The theme shown to viewers who haven't picked one yet.</param>
    /// <returns>The same <see cref="DarkThemeOptions"/> instance, for chaining.</returns>
    public DarkThemeOptions EnableThemeSelector(ReDocTheme defaultTheme = ReDocTheme.Dark)
    {
        if (!Enum.IsDefined(defaultTheme))
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTheme), defaultTheme, null);
        }

        ThemeSelectorEnabled = true;
        DefaultTheme = defaultTheme;
        return this;
    }
}
