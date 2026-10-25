using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// What the options reject, and the startup check this host inherits from core.
/// </summary>
public class ReDocDocumentsOptionsTests
{
    [Fact]
    public void AdditionalConfigJson_ThatIsNotAnObject_ThrowsWhereItIsSet()
    {
        ReDocDocumentsOptions options = new();

        // A bad value here would otherwise reach the page and take the whole config down with it,
        // which is a blank page and one console error. Fail where the mistake is instead.
        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => options.AdditionalConfigJson = "[1, 2]");
        Assert.Equal(nameof(ReDocDocumentsOptions.AdditionalConfigJson), thrown.ParamName);
    }

    [Fact]
    public void AdditionalConfigJson_ThatIsNotJson_ThrowsWhereItIsSet()
    {
        ReDocDocumentsOptions options = new();

        Assert.Throws<ArgumentException>(() => options.AdditionalConfigJson = "{nope}");
    }

    [Fact]
    public void AdditionalConfigJson_IsMergedIntoTheServedConfig()
    {
        ReDocDocumentsOptions options = new()
        {
            AdditionalConfigJson = """{"showExtensions": true}""",
            HideDownloadButton = true,
        };

        string json = ReDocPage.ConfigJson(options);

        Assert.Contains("\"hideDownloadButton\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"showExtensions\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EnableTelemetry_IsOffByDefaultAndSaysSoInTheConfig()
    {
        Assert.Contains(
            "\"disableTelemetry\":true", ReDocPage.ConfigJson(new ReDocDocumentsOptions()),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"disableTelemetry\":false",
            ReDocPage.ConfigJson(new ReDocDocumentsOptions { EnableTelemetry = true }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PrimaryColor_SetsBothVariablesTheAccentComesFrom()
    {
        ReDocDocumentsOptions options = new() { PrimaryColor = "#c026d3" };

        string css = ReDocPage.Css(options, withPicker: false);

        // Two variables, not one: derived tokens fall back to --color-primary-base, but link text
        // reads --link-color-primary and has a default of its own.
        Assert.Contains(
            """
            :root:root {
              --color-primary-base: #c026d3;
              --link-color-primary: #c026d3;
            }
            """,
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PrimaryColor_OutweighsTheVariablesReDocInjectsAtRuntime()
    {
        ReDocDocumentsOptions options = new()
        {
            PrimaryColor = "#c026d3",
            DarkModePrimaryColor = "#e879f9",
        };

        string css = ReDocPage.Css(options, withPicker: false);

        // ReDoc defines its palette on :root and :root.dark from a <style> injected into the head
        // at runtime — after this stylesheet's <link> — so a plain :root here would lose on source
        // order. Measured in the browser: the accent simply didn't apply until these were doubled.
        Assert.Contains(":root:root {", css, StringComparison.Ordinal);
        Assert.Contains(":root:root.dark {", css, StringComparison.Ordinal);
        Assert.DoesNotContain("\n:root {", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkModePrimaryColor_AppliesUnderTheDarkRootOnly()
    {
        ReDocDocumentsOptions options = new()
        {
            PrimaryColor = "#c026d3",
            DarkModePrimaryColor = "#e879f9",
        };

        string css = ReDocPage.Css(options, withPicker: false);

        Assert.Contains(
            ":root:root.dark {\n  --color-primary-base: #e879f9;", css, StringComparison.Ordinal);
        Assert.Contains(
            ":root:root {\n  --color-primary-base: #c026d3;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimaryColor_WhenUnset_WritesNoAccentRule()
    {
        string css = ReDocPage.Css(new ReDocDocumentsOptions(), withPicker: false);

        Assert.DoesNotContain("--color-primary-base: ", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AdditionalCss_ComesAfterTheAccent_SoItWins()
    {
        ReDocDocumentsOptions options = new()
        {
            PrimaryColor = "#c026d3",
            AdditionalCss = ":root { --color-primary-base: rebeccapurple; }",
        };

        string css = ReDocPage.Css(options, withPicker: false);

        Assert.True(
            css.IndexOf("#c026d3", StringComparison.Ordinal)
            < css.IndexOf("rebeccapurple", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("#38bdf8; } body { display: none")]
    [InlineData("red /* nope */")]
    [InlineData("var(--x)\n}")]
    public void PrimaryColor_ThatCouldEscapeItsDeclaration_ThrowsWhereItIsSet(string value)
    {
        ReDocDocumentsOptions options = new();

        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => options.PrimaryColor = value);
        Assert.Equal(nameof(ReDocDocumentsOptions.PrimaryColor), thrown.ParamName);
    }

    [Fact]
    public void PrimaryColor_TakesAnyCssColor()
    {
        // Not a color parser: ReDoc's variables take whatever CSS does, and a build that only
        // understood hex would reject oklch() and the var() someone points at their design tokens.
        ReDocDocumentsOptions options = new()
        {
            PrimaryColor = "oklch(0.7 0.2 320)",
            DarkModePrimaryColor = "var(--brand-accent)",
        };

        string css = ReDocPage.Css(options, withPicker: false);

        Assert.Contains("--link-color-primary: oklch(0.7 0.2 320);", css, StringComparison.Ordinal);
        Assert.Contains("--link-color-primary: var(--brand-accent);", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintStyles_RestoreTheLightRampForTheDarkMode()
    {
        string css = ReDocPage.Css(new ReDocDocumentsOptions(), withPicker: true);

        // ReDoc 3's dark mode is the grey ramp reversed and nothing else, so putting the light
        // ramp back under @media print is what makes a page printed in dark mode legible —
        // browsers drop background colors but keep text colors.
        Assert.Contains("@media print {", css, StringComparison.Ordinal);
        Assert.Contains("--color-warm-grey-11: #1a1c21;", css, StringComparison.Ordinal);

        // ReDoc hides its own sidebar and buttons when printing; the toolbar is ours to hide.
        Assert.Contains(".hlibz-toolbar { display: none !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintStyles_PrintTheLightAccentRatherThanTheDarkOne()
    {
        ReDocDocumentsOptions options = new()
        {
            PrimaryColor = "#c026d3",
            DarkModePrimaryColor = "#e879f9",
        };

        string css = ReDocPage.Css(options, withPicker: false);
        string print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];

        // The dark accent was picked to carry on a near-black page, not on paper.
        Assert.Contains("--link-color-primary: #c026d3;", print, StringComparison.Ordinal);
        Assert.DoesNotContain("#e879f9", print, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintStyles_CanBeTurnedOff()
    {
        string css = ReDocPage.Css(
            new ReDocDocumentsOptions { PrintStyles = false }, withPicker: false);

        Assert.DoesNotContain("@media print", css, StringComparison.Ordinal);
    }

    [Fact]
    public void MapReDocDocuments_WithAnUnregisteredDocumentName_ThrowsAtStartup()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddOpenApi("taxes");
        WebApplication app = builder.Build();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => app.MapReDocDocuments([new ReDocDocument("taxes"), new ReDocDocument("wigets")]));

        Assert.Contains("'wigets'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'taxes'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapReDocDocuments_WithNoDocuments_Throws()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapReDocDocuments([]));
    }

    [Fact]
    public void MapReDocDocuments_WithDuplicateRoutes_Throws()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapReDocDocuments(
            [new ReDocDocument("taxes"), new ReDocDocument("fees", Route: "/api-docs/taxes/")]));
    }
}
