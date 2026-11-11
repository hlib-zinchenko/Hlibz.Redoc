using Microsoft.Playwright;

using Xunit;

namespace Hlibz.Redoc.Extensions.ThemeTests;

/// <summary>
/// The adapters' dark theme as ReDoc 2 actually renders it, on both hosts.
/// </summary>
/// <remarks>
/// <para>
/// The theme's value is CSS fighting colors ReDoc hard-codes outside its theme object, which is
/// fragile by construction and invisible to the suites that check the config and the head content:
/// a bundle update can bring the white back with every one of those still passing. So this reads
/// the computed colors of the elements each override exists for, off the rendered page.
/// </para>
/// <para>
/// Computed values rather than screenshots on purpose: a pixel diff fails on a different OS's font
/// rendering, while <c>getComputedStyle</c> says the same thing on a laptop and on CI.
/// </para>
/// </remarks>
public sealed class DarkThemeRenderingTests(ThemeSites sites) : IAsyncLifetime
{
    /// <summary>
    /// What the dark theme paints, by the element that would show ReDoc's own light value
    /// without it. Each entry is an override in <c>ReDocDarkThemeDefaults</c>.
    /// </summary>
    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["html background"] = "rgb(9, 9, 11)",
        ["body background"] = "rgb(9, 9, 11)",
        ["color-scheme"] = "dark",
        ["scrollbar-color"] = "rgb(63, 63, 70) rgb(24, 24, 27)",
        ["sidebar background"] = "rgb(24, 24, 27)",

        // Hard-coded to a light-theme gray outside the theme object.
        ["section caption"] = "rgb(212, 212, 216)",

        // Every other nesting level is a hard-coded #ffffff without the override.
        ["nested schema wrappers"] = "rgb(24, 24, 27)",

        // Without the override, the label is the panel's own light text on a light background.
        ["selected sample tab"] = "rgb(9, 9, 11)",

        ["inline code"] = "rgb(245, 158, 11) on rgb(39, 39, 42)",
        ["--hlibz-control-bg"] = "rgba(24, 24, 27, 0.92)",
    };

    // Expands POST /widgets' request body a few levels deep, so the nested-schema wrappers the
    // theme recolors are on the page, then reads every color the theme is responsible for. The
    // expanders are labelled "expand properties" on redoc 2.0.0 and "expand {property}" on 2.5.4,
    // hence the prefix. Each is clicked once only: 2.0.0 keeps its label while open, so a second
    // click would fold back up what the first opened.
    private const string ProbeScript = """
        async () => {
          const section = document.querySelector('[data-section-id$="/paths/~1widgets/post"]');
          const clicked = new Set();
          for (let round = 0; round < 6; round++) {
            const fresh = [...section.querySelectorAll('button[aria-label^="expand "]')]
              .filter(button => !clicked.has(button));
            if (fresh.length === 0) { break; }
            fresh.forEach(button => { clicked.add(button); button.click(); });
            await new Promise(resolve => setTimeout(resolve, 250));
          }

          const style = element => getComputedStyle(element);
          const html = style(document.documentElement);
          const nested = [...section.querySelectorAll('table div:has(> table)')];
          const caption = section.querySelector('h5');
          const tab = document.querySelector('.react-tabs__tab--selected'
            + ':not(.tab-success):not(.tab-error):not(.tab-redirect):not(.tab-info)');
          const code = document.querySelector('#redoc-container td code, #redoc-container p code');
          const distinct = values => [...new Set(values)].join(' | ');

          return {
            'html background': html.backgroundColor,
            'body background': style(document.body).backgroundColor,
            'color-scheme': html.colorScheme,
            'scrollbar-color': html.scrollbarColor,
            'sidebar background': style(document.querySelector('.menu-content')).backgroundColor,
            'section caption': caption ? style(caption).color : 'missing',
            'nested schema wrappers': nested.length === 0
              ? 'none rendered'
              : distinct(nested.map(element => style(element).backgroundColor)),
            'selected sample tab': tab ? style(tab).color : 'missing',
            'inline code': code
              ? style(code).color + ' on ' + style(code).backgroundColor
              : 'missing',
            '--hlibz-control-bg': html.getPropertyValue('--hlibz-control-bg').trim(),
            'oneOf buttons': distinct([...section.querySelectorAll('span + div > button')]
              .map(button => style(button).backgroundColor)),
          };
        }
        """;

    private IBrowserContext context = null!;

    public async ValueTask InitializeAsync() =>
        context = await sites.Browser.NewContextAsync(
            new() { ViewportSize = new() { Width = 1280, Height = 900 } });

    public async ValueTask DisposeAsync() => await context.DisposeAsync();

    [Theory]
    [InlineData(ReDocHost.RedocAspNetCore)]
    [InlineData(ReDocHost.Swashbuckle)]
    public async Task DarkTheme_PaintsWhatReDocHardCodes(ReDocHost host)
    {
        Dictionary<string, string> rendered = await ProbeAsync(host, "widgets");

        foreach ((string element, string expected) in DarkPalette)
        {
            // One at a time, so a failure names the element whose override stopped working.
            Assert.True(
                rendered.TryGetValue(element, out string? actual) && actual == expected,
                $"{host}: {element} rendered as '{actual}', expected '{expected}'.");
        }
    }

    [Fact]
    public async Task BothHosts_RenderTheThemeIdentically()
    {
        Dictionary<string, string> redoc200 =
            await ProbeAsync(ReDocHost.RedocAspNetCore, "widgets");
        Dictionary<string, string> redoc254 =
            await ProbeAsync(ReDocHost.Swashbuckle, "widgets");

        // redoc 2.0.0 and 2.5.4 are different bundles under one theme. Identical here covers what
        // the palette above doesn't pin, the oneOf switch included, so a host difference shows
        // up as a failure rather than in a screenshot nobody compared.
        Assert.Equal(redoc200, redoc254);
    }

    [Theory]
    [InlineData(ReDocHost.RedocAspNetCore)]
    [InlineData(ReDocHost.Swashbuckle)]
    public async Task ThemeSelector_SwitchesALightPageToTheDarkTheme(ReDocHost host)
    {
        IPage page = await OpenAsync(host, ThemeSites.SelectorDocument);
        string sidebar = await SidebarBackgroundAsync(page);

        Assert.Equal(
            "light",
            await page.EvaluateAsync<string>(
                "() => document.documentElement.getAttribute('data-redoc-theme')"));
        Assert.NotEqual(DarkPalette["sidebar background"], sidebar);

        // The dark CSS applies at once; the sidebar's color comes from ReDoc's theme config,
        // so it only changes once the selector has torn ReDoc down and started it again — and
        // for a moment in between there is no sidebar at all.
        await page.ClickAsync("button[aria-label='Dark theme']");
        await page.WaitForFunctionAsync(
            """
            expected => {
              const sidebar = document.querySelector('.menu-content');
              return sidebar !== null && getComputedStyle(sidebar).backgroundColor === expected;
            }
            """,
            DarkPalette["sidebar background"]);

        Assert.Equal(
            DarkPalette["body background"],
            await page.EvaluateAsync<string>(
                "() => getComputedStyle(document.body).backgroundColor"));
    }

    private async Task<Dictionary<string, string>> ProbeAsync(ReDocHost host, string document)
    {
        IPage page = await OpenAsync(host, document);
        await page.WaitForSelectorAsync("[data-section-id$='/paths/~1widgets/post']");
        return await page.EvaluateAsync<Dictionary<string, string>>(ProbeScript);
    }

    private async Task<IPage> OpenAsync(ReDocHost host, string document)
    {
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        await page.GotoAsync($"{sites.AddressOf(host)}/api-docs/{document}/index.html");
        await page.WaitForSelectorAsync(".menu-content");
        return page;
    }

    private static Task<string> SidebarBackgroundAsync(IPage page) =>
        page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('.menu-content')).backgroundColor");
}
