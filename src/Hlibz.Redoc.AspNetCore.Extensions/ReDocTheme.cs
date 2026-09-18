namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// A theme selectable from the ReDoc UI when the theme selector is enabled via
/// <see cref="DarkThemeOptions.EnableThemeSelector"/>.
/// </summary>
public enum ReDocTheme
{
    /// <summary>This package's dark theme.</summary>
    Dark,

    /// <summary>ReDoc's stock (light) theme, exactly as it renders without this package.</summary>
    Light,

    /// <summary>
    /// Follows the viewer's OS/browser light/dark preference (<c>prefers-color-scheme</c>), live.
    /// </summary>
    System,
}
