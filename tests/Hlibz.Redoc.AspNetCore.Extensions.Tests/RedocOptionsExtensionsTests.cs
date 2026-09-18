using Redoc.AspNetCore;
using Xunit;

namespace Hlibz.Redoc.AspNetCore.Extensions.Tests;

public class RedocOptionsExtensionsTests
{
    [Fact]
    public void UseDarkTheme_WithDefaults_SetsThemeConfigOnAdditionalItems()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        object theme = Assert.Contains("theme", options.ConfigObject.AdditionalItems);
        Dictionary<string, object> themeDict = Assert.IsType<Dictionary<string, object>>(theme);
        Assert.Contains("colors", themeDict);
        Assert.Contains("sidebar", themeDict);
        Assert.Contains("rightPanel", themeDict);
        Assert.Contains("codeBlock", themeDict);
        Assert.Contains("typography", themeDict);
        Assert.Contains("schema", themeDict);
    }

    [Fact]
    public void UseDarkTheme_WithDefaults_SetsHeadContent()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.NotNull(options.HeadContent);
        Assert.Contains("""color-scheme" content="dark""", options.HeadContent);
        Assert.Contains("<style>", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithCustomPrimaryColor_OverridesColorsPrimaryMain()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.PrimaryColor = "#a78bfa");

        Dictionary<string, object> themeDict =
            (Dictionary<string, object>)options.ConfigObject.AdditionalItems["theme"];
        Dictionary<string, object> colors = (Dictionary<string, object>)themeDict["colors"];
        Dictionary<string, object> primary = (Dictionary<string, object>)colors["primary"];

        Assert.Equal("#a78bfa", primary["main"]);
    }

    [Fact]
    public void UseDarkTheme_WithAdditionalCss_AppendsToHeadContent()
    {
        ReDocOptions options = new();
        const string customCss = ".my-custom-rule { color: red; }";

        options.UseDarkTheme(theme => theme.AdditionalCss = customCss);

        Assert.Contains(customCss, options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithoutThemeSelector_EmitsNoScript()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.DoesNotContain("<script>", options.HeadContent);
        Assert.DoesNotContain("data-redoc-theme", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelector_ScopesDarkCssAndEmitsSelectorScript()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector());

        Assert.Contains("""html[data-redoc-theme="dark"] #redoc-container h5""", options.HeadContent);
        Assert.DoesNotContain("""<meta name="color-scheme" content="dark">""", options.HeadContent);
        Assert.Contains("<script>", options.HeadContent);
        Assert.Contains("readStoredPreference() || 'dark'", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelector_StillShipsDarkThemeConfig()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector(ReDocTheme.Light));

        Assert.Contains("theme", options.ConfigObject.AdditionalItems);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelectorDefaultLight_PreselectsLight()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector(ReDocTheme.Light));

        Assert.Contains("readStoredPreference() || 'light'", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelectorDefaultSystem_PreselectsSystem()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector(ReDocTheme.System));

        Assert.Contains("readStoredPreference() || 'system'", options.HeadContent);
    }

    [Fact]
    public void EnableThemeSelector_WithUndefinedTheme_Throws()
    {
        DarkThemeOptions theme = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => theme.EnableThemeSelector((ReDocTheme)42));
    }

    [Fact]
    public void UseDarkTheme_ReturnsSameOptionsInstance_ForChaining()
    {
        ReDocOptions options = new();

        ReDocOptions result = options.UseDarkTheme();

        Assert.Same(options, result);
    }
}
