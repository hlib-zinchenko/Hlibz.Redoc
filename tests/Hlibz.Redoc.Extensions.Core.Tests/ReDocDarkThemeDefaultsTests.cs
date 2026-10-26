using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// The two things every adapter hands its host: the head content string and the ReDoc theme
/// config. Reached directly rather than through a host's <c>UseDarkTheme</c>, so these assertions
/// hold for all of them.
/// </summary>
public class ReDocDarkThemeDefaultsTests
{
    [Fact]
    public void BuildConfigValue_WithDefaults_CoversEveryThemeGroup()
    {
        Dictionary<string, object> theme = Config(_ => { });

        Assert.Contains("colors", theme);
        Assert.Contains("sidebar", theme);
        Assert.Contains("rightPanel", theme);
        Assert.Contains("codeBlock", theme);
        Assert.Contains("typography", theme);
        Assert.Contains("schema", theme);
    }

    [Fact]
    public void BuildHeadContent_WithDefaults_DeclaresDarkColorSchemeAndStyles()
    {
        string head = Head(_ => { });

        Assert.Contains("""color-scheme" content="dark""", head);
        Assert.Contains("<style>", head);
    }

    [Fact]
    public void BuildConfigValue_WithCustomPrimaryColor_OverridesColorsPrimaryMain()
    {
        Dictionary<string, object> theme = Config(t => t.PrimaryColor = "#a78bfa");

        Dictionary<string, object> colors = (Dictionary<string, object>)theme["colors"];
        Dictionary<string, object> primary = (Dictionary<string, object>)colors["primary"];

        Assert.Equal("#a78bfa", primary["main"]);
    }

    [Fact]
    public void BuildHeadContent_WithAdditionalCss_AppendsIt()
    {
        const string customCss = ".my-custom-rule { color: red; }";

        Assert.Contains(customCss, Head(t => t.AdditionalCss = customCss));
    }

    [Fact]
    public void BuildHeadContent_WithoutThemeSelector_EmitsNoScript()
    {
        string head = Head(_ => { });

        Assert.DoesNotContain("<script>", head);
        Assert.DoesNotContain("data-redoc-theme", head);
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelector_ScopesDarkCssAndEmitsSelectorScript()
    {
        string head = Head(t => t.EnableThemeSelector());

        Assert.Contains("""html[data-redoc-theme="dark"] #redoc-container h5""", head);
        Assert.DoesNotContain("""<meta name="color-scheme" content="dark">""", head);
        Assert.Contains("<script>", head);
        Assert.Contains("readStoredPreference() || 'dark'", head);
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelector_DeclaresLightAndDarkControlTokens()
    {
        string head = Head(t => t.EnableThemeSelector());

        // The selector's CSS has no fallbacks, so without the picker on the page these
        // declarations are the only source of its colors.
        Assert.Contains(":where(html) {", head);
        Assert.Contains("--hlibz-control-accent: #32329f", head);
        Assert.Contains("--hlibz-control-accent-fg: #09090b", head);
    }

    [Fact]
    public void BuildConfigValue_WithThemeSelectorDefaultingToLight_StillShipsTheDarkPalette()
    {
        // The selector switches to the dark config at runtime, so it has to be shipped either way.
        Dictionary<string, object> theme = Config(t => t.EnableThemeSelector(ReDocTheme.Light));

        Assert.Contains("colors", theme);
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelectorDefaultLight_PreselectsLight()
    {
        Assert.Contains(
            "readStoredPreference() || 'light'",
            Head(t => t.EnableThemeSelector(ReDocTheme.Light)));
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelectorDefaultSystem_PreselectsSystem()
    {
        Assert.Contains(
            "readStoredPreference() || 'system'",
            Head(t => t.EnableThemeSelector(ReDocTheme.System)));
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelectorAndFonts_KeepsTheFontsInTheLightTheme()
    {
        string head = Head(t =>
        {
            t.EnableThemeSelector();
            t.FontFamily = "Inter, sans-serif";
        });

        // Light drops the dark theme config, so the fonts have to reach ReDoc separately.
        Assert.Contains(
            """var lightThemeConfig = {"typography":{"fontFamily":"Inter, sans-serif"}};""", head);
    }

    [Fact]
    public void BuildHeadContent_WithThemeSelectorAndNoFonts_HasNoLightThemeConfig()
    {
        Assert.Contains("var lightThemeConfig = null;", Head(t => t.EnableThemeSelector()));
    }

    [Fact]
    public void BuildHeadContent_WithFavicon_EmitsAnIconLink()
    {
        Assert.Contains(
            """<link rel="icon" href="/favicon.svg?v=2&amp;x=1">""",
            Head(t => t.Favicon = "/favicon.svg?v=2&x=1"));
    }

    [Fact]
    public void BuildHeadContent_WithoutFavicon_EmitsNoIconLink()
    {
        Assert.DoesNotContain("rel=\"icon\"", Head(_ => { }));
    }

    [Fact]
    public void BuildHeadContent_WithDefaults_EmitsPrintStyles()
    {
        string head = Head(_ => { });

        Assert.Contains("@media print {", head);
        Assert.Contains("color-scheme: light !important", head);
    }

    [Fact]
    public void BuildHeadContent_WithPrintStylesOff_EmitsNone()
    {
        Assert.DoesNotContain("@media print", Head(t => t.PrintStyles = false));
    }

    [Fact]
    public void BuildHeadContent_PutsAdditionalCssAfterThePrintStyles()
    {
        const string customCss = "@media print { .mine { display: none; } }";
        string head = Head(t => t.AdditionalCss = customCss);

        // Equal specificity, so a caller's print rules have to come last to win.
        Assert.True(
            head.IndexOf("color-scheme: light", StringComparison.Ordinal)
            < head.IndexOf(customCss, StringComparison.Ordinal));
    }

    private static string Head(Action<DarkThemeOptions> configure)
    {
        DarkThemeOptions theme = new();
        configure(theme);
        return ReDocDarkThemeDefaults.BuildHeadContent(theme);
    }

    private static Dictionary<string, object> Config(Action<DarkThemeOptions> configure)
    {
        DarkThemeOptions theme = new();
        configure(theme);
        return ReDocDarkThemeDefaults.BuildConfigValue(theme);
    }
}
