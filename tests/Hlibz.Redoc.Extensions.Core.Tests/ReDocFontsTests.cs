using System.Text;
using System.Text.Json;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// Font handling, which is the one theme setting whose value is free text and therefore has to
/// survive the JavaScript string literal ReDoc's page carries the config in.
/// </summary>
public class ReDocFontsTests
{
    [Fact]
    public void BuildConfigValue_WithFontFamilies_PutsThemInTypographyAlongsideTheColors()
    {
        Dictionary<string, object> theme = Config(t =>
        {
            t.FontFamily = "Inter, sans-serif";
            t.HeadingsFontFamily = "Lora, serif";
            t.CodeFontFamily = "Fira Code, monospace";
        });

        Dictionary<string, object> typography = (Dictionary<string, object>)theme["typography"];
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
    public void BuildConfigValue_WithoutFontFamilies_LeavesTypographyFontsToReDoc()
    {
        Dictionary<string, object> typography =
            (Dictionary<string, object>)Config(_ => { })["typography"];

        Assert.DoesNotContain("fontFamily", typography);
        Assert.DoesNotContain("headings", typography);
    }

    [Fact]
    public void BuildConfigValue_WithQuotedFontNames_DropsTheQuotes()
    {
        Dictionary<string, object> typography = (Dictionary<string, object>)
            Config(t => t.FontFamily = """ 'Fira Sans', "Noto Sans", serif """)["typography"];

        // ReDoc's page carries the config inside a JS string literal, so a quote — which
        // JsonSerializer escapes — would come back out bare and break JSON.parse on the whole
        // config. CSS doesn't need the quotes here, so they go.
        Assert.Equal("Fira Sans, Noto Sans, serif", typography["fontFamily"]);
    }

    [Fact]
    public void BuildConfigValue_WithAFontNameThePageCantCarry_ThrowsNamingTheProperty()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => Config(t => t.HeadingsFontFamily = "Noto Sans JP, ヒラギノ"));

        Assert.Equal("HeadingsFontFamily", error.ParamName);
    }

    [Fact]
    public void UseSystemFonts_UsesAFontStackThePageCanCarry()
    {
        Dictionary<string, object> theme = Config(t => t.UseSystemFonts());

        // A \uXXXX escape anywhere in the config takes the page down, so the built-in stacks must
        // not need one: this is the shape ReDoc's index.html embeds them in.
        string config = JsonSerializer.Serialize(theme, new JsonSerializerOptions());
        Assert.DoesNotContain("\\u", config);
    }

    [Fact]
    public void WithoutWebFontLinks_StripsBothGoogleFontHostsAndLeavesTheRestAlone()
    {
        Func<Stream> source = () => new MemoryStream(Encoding.UTF8.GetBytes(
            """
            <head>
              <link rel="preconnect" href="https://fonts.gstatic.com">
              <link href="https://fonts.googleapis.com/css?family=Roboto" rel="stylesheet">
              <link rel="stylesheet" href="/mine.css">
            </head>
            """));

        Func<Stream> stripped = ReDocFonts.WithoutWebFontLinks(source);

        string index = Read(stripped);
        Assert.DoesNotContain("fonts.googleapis.com", index);
        Assert.DoesNotContain("fonts.gstatic.com", index);
        Assert.Contains("/mine.css", index);
    }

    private static Dictionary<string, object> Config(Action<DarkThemeOptions> configure)
    {
        DarkThemeOptions theme = new();
        configure(theme);
        return ReDocDarkThemeDefaults.BuildConfigValue(theme);
    }

    private static string Read(Func<Stream> indexStream)
    {
        using Stream stream = indexStream();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
