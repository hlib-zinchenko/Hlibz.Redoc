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

    /// <summary>
    /// Font stack for body text, as a CSS <c>font-family</c> value. When not set, ReDoc's own
    /// default (Roboto) applies. See <see cref="UseSystemFonts"/>.
    /// </summary>
    /// <remarks>
    /// A stack is plain ASCII family names separated by commas, e.g.
    /// <c>"Inter, system-ui, sans-serif"</c>. Quotes around a name are allowed but dropped — CSS
    /// doesn't need them, and ReDoc's page can't carry them — and anything else outside letters,
    /// digits, <c>-</c>, <c>_</c>, <c>.</c>, spaces and commas throws, rather than breaking the
    /// page at runtime.
    /// </remarks>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Font stack for headings. When not set, ReDoc's own default (Montserrat) applies.
    /// </summary>
    public string? HeadingsFontFamily { get; set; }

    /// <summary>
    /// Font stack for code samples, schema types and inline code. When not set, ReDoc's own
    /// default (Courier) applies.
    /// </summary>
    public string? CodeFontFamily { get; set; }

    /// <summary>
    /// Whether the page keeps ReDoc's stock <c>&lt;link&gt;</c> to <c>fonts.googleapis.com</c>,
    /// which pulls Montserrat and Roboto at runtime. Defaults to <see langword="true"/>, matching
    /// ReDoc. Set to <see langword="false"/> to serve the page without any third-party request —
    /// for a <c>style-src</c>/<c>font-src</c> CSP that doesn't allow Google's hosts, for an
    /// offline or air-gapped deployment, or to keep viewers' IP addresses away from Google.
    /// </summary>
    /// <remarks>
    /// Dropping the link rewrites ReDoc's <c>index.html</c>, because the stock template has it
    /// above the content this package injects. ReDoc still *asks* for Montserrat and Roboto by
    /// name unless the font families above are set, so on its own this falls back to whatever the
    /// viewer happens to have installed. <see cref="UseSystemFonts"/> does both at once.
    /// </remarks>
    public bool LoadGoogleFonts { get; set; } = true;

    /// <summary>
    /// URL of a favicon for the documentation pages, emitted as
    /// <c>&lt;link rel="icon"&gt;</c>. Browsers infer the format, so <c>.ico</c>, <c>.png</c> and
    /// <c>.svg</c> all work. ReDoc's stock page declares none, so browsers fall back to
    /// <c>/favicon.ico</c>.
    /// </summary>
    /// <remarks>
    /// Use an absolute URL or a root-relative path (<c>/favicon.svg</c>). The pages are served at
    /// <c>/{route}/index.html</c>, so a document-relative path would resolve against the page's
    /// own folder rather than the app root.
    /// </remarks>
    public string? Favicon { get; set; }

    /// <summary>
    /// Whether a print stylesheet is included, which prints the documentation on white paper with
    /// black text and hides the sidebar and this package's controls. Defaults to
    /// <see langword="true"/>: without it a dark page prints either as a black rectangle or as
    /// near-white text on white paper, depending on the browser's "background graphics" setting.
    /// </summary>
    public bool PrintStyles { get; set; } = true;

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

    /// <summary>
    /// Renders the documentation in the viewer's own OS fonts and stops the page from requesting
    /// Montserrat and Roboto from <c>fonts.googleapis.com</c>: it sets the three font families to
    /// system stacks and clears <see cref="LoadGoogleFonts"/>. The page then makes no third-party
    /// request at all, which is what a strict CSP, an air-gapped deployment and EU privacy rules
    /// each want.
    /// </summary>
    /// <remarks>
    /// Assign any of the font properties after this call to keep the rest of the behavior while
    /// naming your own (self-hosted) stack.
    /// </remarks>
    /// <returns>The same <see cref="DarkThemeOptions"/> instance, for chaining.</returns>
    public DarkThemeOptions UseSystemFonts()
    {
        FontFamily = ReDocFonts.SystemSansSerif;
        HeadingsFontFamily = ReDocFonts.SystemSansSerif;
        CodeFontFamily = ReDocFonts.SystemMonospace;
        LoadGoogleFonts = false;
        return this;
    }
}
