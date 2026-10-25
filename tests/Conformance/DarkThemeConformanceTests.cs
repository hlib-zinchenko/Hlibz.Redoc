using System.Text;

using Xunit;

namespace Hlibz.Redoc.Extensions.Conformance;

/// <summary>
/// What <c>UseDarkTheme</c> does to a host's own options: the two seams it writes to, and the
/// index-page rewrite, run against that host's <em>actual</em> stock <c>index.html</c>. The content
/// of the head string and the theme config is asserted host-free in
/// <c>Hlibz.Redoc.Extensions.Tests</c>.
/// </summary>
public class DarkThemeConformanceTests
{
    [Fact]
    public void UseDarkTheme_PutsTheThemeConfigOnAdditionalItems()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        object theme = Assert.Contains("theme", options.ConfigObject.AdditionalItems);
        Assert.IsType<Dictionary<string, object>>(theme);
    }

    [Fact]
    public void UseDarkTheme_SetsHeadContent()
    {
        ReDocOptions options = new();

        options.UseDarkTheme();

        Assert.NotNull(options.HeadContent);
        Assert.Contains("<style>", options.HeadContent);
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
    public void UseDarkTheme_ReturnsSameOptionsInstance_ForChaining()
    {
        ReDocOptions options = new();

        ReDocOptions result = options.UseDarkTheme();

        Assert.Same(options, result);
    }

    private static string ReadIndex(ReDocOptions options)
    {
        using Stream stream = options.IndexStream();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
