using System.Text;
using System.Text.Json;

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
    public void UseDarkTheme_WithThemeSelector_DeclaresLightAndDarkControlTokens()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector());

        // The selector's CSS has no fallbacks, so without the picker on the page these
        // declarations are the only source of its colors.
        Assert.Contains(":where(html) {", options.HeadContent);
        Assert.Contains("--hlibz-control-accent: #32329f", options.HeadContent);
        Assert.Contains("--hlibz-control-accent-fg: #09090b", options.HeadContent);
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
    public void UseDarkTheme_WithFontFamilies_PutsThemInTypographyAlongsideTheColors()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme =>
        {
            theme.FontFamily = "Inter, sans-serif";
            theme.HeadingsFontFamily = "Lora, serif";
            theme.CodeFontFamily = "Fira Code, monospace";
        });

        Dictionary<string, object> themeDict =
            (Dictionary<string, object>)options.ConfigObject.AdditionalItems["theme"];
        Dictionary<string, object> typography = (Dictionary<string, object>)themeDict["typography"];
        Dictionary<string, object> headings = (Dictionary<string, object>)typography["headings"];
        Dictionary<string, object> code = (Dictionary<string, object>)typography["code"];

        Assert.Equal("Inter, sans-serif", typography["fontFamily"]);
        Assert.Equal("Lora, serif", headings["fontFamily"]);
        Assert.Equal("Fira Code, monospace", code["fontFamily"]);
        // The code group also carries the theme's colors; neither feature may drop the other's.
        Assert.Equal("#f59e0b", code["color"]);
        Assert.Contains("links", typography);
    }

    [Fact]
    public void UseDarkTheme_WithoutFontFamilies_LeavesTypographyFontsToReDoc()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Dictionary<string, object> themeDict =
            (Dictionary<string, object>)options.ConfigObject.AdditionalItems["theme"];
        Dictionary<string, object> typography = (Dictionary<string, object>)themeDict["typography"];

        Assert.DoesNotContain("fontFamily", typography);
        Assert.DoesNotContain("headings", typography);
    }

    [Fact]
    public void UseDarkTheme_WithQuotedFontNames_DropsTheQuotes()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.FontFamily = """ 'Fira Sans', "Noto Sans", serif """);

        Dictionary<string, object> themeDict =
            (Dictionary<string, object>)options.ConfigObject.AdditionalItems["theme"];
        Dictionary<string, object> typography = (Dictionary<string, object>)themeDict["typography"];

        // ReDoc's page carries the config inside a JS string literal, so a quote — which
        // JsonSerializer escapes — would come back out bare and break JSON.parse on the whole
        // config. CSS doesn't need the quotes here, so they go.
        Assert.Equal("Fira Sans, Noto Sans, serif", typography["fontFamily"]);
    }

    [Fact]
    public void UseDarkTheme_WithAFontNameThePageCantCarry_ThrowsNamingTheProperty()
    {
        ReDocOptions options = new();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => options.UseDarkTheme(
                theme => theme.HeadingsFontFamily = "Noto Sans JP, ヒラギノ"));

        Assert.Equal("HeadingsFontFamily", error.ParamName);
    }

    [Fact]
    public void UseSystemFonts_UsesAFontStackThePageCanCarry()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.UseSystemFonts());

        // A \uXXXX escape anywhere in the config takes the page down, so the built-in stacks must
        // not need one: this is the shape ReDoc's index.html embeds them in.
        string config = JsonSerializer.Serialize(
            options.ConfigObject.AdditionalItems["theme"],
            new JsonSerializerOptions());
        Assert.DoesNotContain("\\u", config);
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

    [Fact]
    public void UseDarkTheme_WithGoogleFonts_LeavesTheIndexPageAlone()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.Contains("fonts.googleapis.com", ReadIndex(options));
    }

    [Fact]
    public void UseDarkTheme_WithoutGoogleFonts_StripsTheFontLinkFromTheIndexPage()
    {
        ReDocOptions options = new();
        string before = ReadIndex(options);

        options.UseDarkTheme(theme => theme.LoadGoogleFonts = false);

        // The link has to go from the page itself: it sits above %(HeadContent) in the template,
        // so a script injected there runs after the browser has already asked for the fonts.
        Assert.Contains("fonts.googleapis.com", before);
        string after = ReadIndex(options);
        Assert.DoesNotContain("fonts.googleapis.com", after);
        Assert.Contains("%(HeadContent)", after);
        Assert.Contains("redoc.standalone.js", after);
    }

    [Fact]
    public void UseDarkTheme_WithoutGoogleFonts_KeepsWorkingAcrossRequests()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.UseSystemFonts());

        // The middleware calls IndexStream once per request, so the wrapper can't be single-use.
        Assert.Equal(ReadIndex(options), ReadIndex(options));
    }

    [Fact]
    public void UseDarkTheme_WithoutGoogleFonts_WrapsACustomIndexStream()
    {
        ReDocOptions options = new();
        options.IndexStream = () => new MemoryStream(Encoding.UTF8.GetBytes(
            """
            <head>
              <link href="https://fonts.googleapis.com/css?family=Roboto" rel="stylesheet">
              <!-- mine -->
            </head>
            """));

        options.UseDarkTheme(theme => theme.UseSystemFonts());

        string index = ReadIndex(options);
        Assert.DoesNotContain("fonts.googleapis.com", index);
        Assert.Contains("<!-- mine -->", index);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelectorAndFonts_KeepsTheFontsInTheLightTheme()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme =>
        {
            theme.EnableThemeSelector();
            theme.FontFamily = "Inter, sans-serif";
        });

        // Light drops the dark theme config, so the fonts have to reach ReDoc separately.
        Assert.Contains(
            """var lightThemeConfig = {"typography":{"fontFamily":"Inter, sans-serif"}};""",
            options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithThemeSelectorAndNoFonts_HasNoLightThemeConfig()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.EnableThemeSelector());

        Assert.Contains("var lightThemeConfig = null;", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithFavicon_EmitsAnIconLink()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.Favicon = "/favicon.svg?v=2&x=1");

        Assert.Contains(
            """<link rel="icon" href="/favicon.svg?v=2&amp;x=1">""", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithoutFavicon_EmitsNoIconLink()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.DoesNotContain("rel=\"icon\"", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithDefaults_EmitsPrintStyles()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.Contains("@media print {", options.HeadContent);
        Assert.Contains("color-scheme: light !important", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_WithPrintStylesOff_EmitsNone()
    {
        ReDocOptions options = new();

        options.UseDarkTheme(theme => theme.PrintStyles = false);

        Assert.DoesNotContain("@media print", options.HeadContent);
    }

    [Fact]
    public void UseDarkTheme_PutsAdditionalCssAfterThePrintStyles()
    {
        ReDocOptions options = new();
        const string customCss = "@media print { .mine { display: none; } }";

        options.UseDarkTheme(theme => theme.AdditionalCss = customCss);

        // Equal specificity, so a caller's print rules have to come last to win.
        Assert.True(
            options.HeadContent.IndexOf("color-scheme: light", StringComparison.Ordinal)
            < options.HeadContent.IndexOf(customCss, StringComparison.Ordinal));
    }

    private static string ReadIndex(ReDocOptions options)
    {
        using Stream stream = options.IndexStream();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void UseDarkTheme_ReturnsSameOptionsInstance_ForChaining()
    {
        ReDocOptions options = new();

        ReDocOptions result = options.UseDarkTheme();

        Assert.Same(options, result);
    }
}
