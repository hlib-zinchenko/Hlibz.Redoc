using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>The options object's own behaviour, before anything is built from it.</summary>
public class DarkThemeOptionsTests
{
    [Fact]
    public void EnableThemeSelector_WithUndefinedTheme_Throws()
    {
        DarkThemeOptions theme = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => theme.EnableThemeSelector((ReDocTheme)42));
    }

    [Fact]
    public void UseSystemFonts_SetsSystemStacksAndStopsLoadingGoogleFonts()
    {
        DarkThemeOptions theme = new();

        DarkThemeOptions result = theme.UseSystemFonts();

        Assert.Same(theme, result);
        Assert.False(theme.LoadGoogleFonts);
        Assert.Contains("system-ui", theme.FontFamily);
        Assert.Contains("system-ui", theme.HeadingsFontFamily);
        Assert.Contains("monospace", theme.CodeFontFamily);
    }
}
