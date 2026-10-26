using System.Text.Json;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Options for <see cref="ReDocDocumentsBuilderExtensions.MapReDocDocuments"/> and
/// <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>. Everything up to
/// <see cref="DefaultColorMode"/> comes from <see cref="ReDocDocumentsOptionsBase"/> and is shared
/// with the adapters for Redoc.AspNetCore and Swashbuckle.
/// </summary>
/// <remarks>
/// Where an adapter hands you its host's <c>ReDocOptions</c> through a <c>ConfigureReDoc</c>
/// callback, this package has no host to configure — it <em>is</em> the host — so the page's own
/// settings are properties here.
/// </remarks>
public sealed class ReDocDocumentsOptions : ReDocDocumentsOptionsBase
{
    private string? additionalConfigJson;
    private string? primaryColor;
    private string? darkModePrimaryColor;
    private string? contentSecurityPolicy;

    /// <summary>
    /// The policy these pages are known to render under, measured rather than reasoned about:
    /// every allowance beyond <c>default-src 'self'</c> is there because a blocked request said
    /// so, and <c>samples/Sample.Own</c> serves its pages under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no nonce in it and no <c>'unsafe-inline'</c> in <c>script-src</c>, because nothing
    /// on the page is inline — that is the whole reason this package owns the template.
    /// <c>style-src 'unsafe-inline'</c> is not negotiable: ReDoc renders through
    /// styled-components, which injects rules at runtime, and a nonce does not reach them.
    /// </para>
    /// <para>
    /// Two allowances depend on things outside this package: <c>img-src</c> names
    /// <c>cdn.redoc.ly</c>, where ReDoc's own bundle fetches the "API docs by Redocly" badge, and
    /// <c>connect-src 'self'</c> assumes the spec is served from this origin. A spec on another
    /// origin needs that origin added.
    /// </para>
    /// </remarks>
    public static string DefaultContentSecurityPolicy { get; } =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: https://cdn.redoc.ly; font-src 'self' data:; "
        + "worker-src 'self' blob:; child-src 'self' blob:; connect-src 'self'; "
        + "object-src 'none'; base-uri 'none'";

    /// <summary>
    /// Whether each page serves its own OpenAPI document, at <c>openapi.json</c> beside the page,
    /// instead of reading it from wherever <see cref="ReDocDocumentsOptionsBase.SpecUrlTemplate"/>
    /// points. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Turn this on to put a document behind a policy. The spec is then one of that document's own
    /// endpoints, so
    /// <c>MapReDocDocuments(...).ForDocument("internal").RequireAuthorization()</c> covers the page
    /// <em>and</em> the document it renders. Without it, the page is gated and the spec is still
    /// served by whoever mapped <c>MapOpenApi</c> or <c>MapSwagger</c> — one route for every
    /// document, with nothing per-document to attach a policy to — so an anonymous request reads
    /// the whole API surface as JSON. Hiding a spec is discretion rather than access control
    /// either way: the endpoints it describes need their own authorization.
    /// </para>
    /// <para>
    /// The document is generated through the same provider <c>dotnet getdocument</c> uses, which
    /// both <c>AddOpenApi</c> and Swashbuckle's <c>AddSwaggerGen</c> register, so neither
    /// <c>MapOpenApi()</c> nor <c>UseSwagger()</c> needs to be mapped at all. A document no
    /// provider offers throws when the pages are mapped. One with an explicit
    /// <see cref="ReDocDocument.SpecUrl"/> is left alone — it says where its spec is — and
    /// setting <see cref="ReDocDocumentsOptionsBase.SpecUrlTemplate"/> as well throws, because
    /// then two settings disagree about where every page reads its spec from.
    /// </para>
    /// <para>
    /// A generated spec is not cached: it is built per request, like <c>MapOpenApi</c>'s, and sent
    /// <c>no-store</c> so a shared proxy can't hand a gated document to the next caller.
    /// </para>
    /// </remarks>
    public bool ServeSpecs { get; set; }

    /// <summary>
    /// The color mode a first-time viewer sees. Defaults to <see cref="ReDocColorMode.System"/>.
    /// ReDoc keeps the viewer's own choice in <c>localStorage</c> and that always wins.
    /// </summary>
    public ReDocColorMode DefaultColorMode { get; set; } = ReDocColorMode.System;

    /// <summary>
    /// <c>href</c> for the page's <c>&lt;link rel="icon"&gt;</c>. No icon is declared when this is
    /// <see langword="null"/>, which leaves the browser asking for <c>/favicon.ico</c>.
    /// </summary>
    public string? Favicon { get; set; }

    /// <summary>
    /// The page's accent color, as any CSS color value. Applies to both color modes unless
    /// <see cref="DarkModePrimaryColor"/> is also set; <see langword="null"/>, the default, leaves
    /// ReDoc's own accent alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ReDoc's accent is not a single variable: this sets <c>--color-primary-base</c>, which
    /// derived tokens fall back to (primary buttons, link underlines, deep-link icons), and
    /// <c>--link-color-primary</c>, which link <em>text</em> reads and which has a default of its
    /// own. For anything further, <see cref="AdditionalCss"/> can reach every one of ReDoc's
    /// variables; this property is here because the accent is what almost everyone wants and
    /// finding those two names took a session in devtools.
    /// </para>
    /// <para>
    /// The value lands in a stylesheet, so it is checked when set: anything that could end the
    /// declaration or open a new rule throws here rather than serving a broken page.
    /// </para>
    /// </remarks>
    public string? PrimaryColor
    {
        get => primaryColor;
        set => primaryColor = ValidateColor(value, nameof(PrimaryColor));
    }

    /// <summary>
    /// The accent color in dark mode, when it should differ from <see cref="PrimaryColor"/> — a
    /// hue picked for a white page is usually too dark on a near-black one. When
    /// <see langword="null"/>, <see cref="PrimaryColor"/> applies to both modes.
    /// </summary>
    /// <remarks>
    /// Named for the color mode, not for a shade: unlike the adapters'
    /// <c>DarkThemeOptions.PrimaryColorDark</c>, which is a darker shade of one accent, this is
    /// the whole accent while <c>html.dark</c> is in effect.
    /// </remarks>
    public string? DarkModePrimaryColor
    {
        get => darkModePrimaryColor;
        set => darkModePrimaryColor = ValidateColor(value, nameof(DarkModePrimaryColor));
    }

    /// <summary>
    /// A named palette for ReDoc's dark mode. Defaults to <see cref="ReDocDarkThemePreset.None"/>,
    /// which leaves ReDoc's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A preset re-points ReDoc 3's grey ramp, which every surface, border and piece of text is
    /// derived from, so it is a whole dark theme rather than a tint. It applies to dark mode only:
    /// all of them are dark palettes, and a light counterpart invented here would be nobody's
    /// theme. Set <see cref="DefaultColorMode"/> to <see cref="ReDocColorMode.Dark"/> if a
    /// first-time viewer on a light OS should see it too.
    /// </para>
    /// <para>
    /// <see cref="PrimaryColor"/> or <see cref="DarkModePrimaryColor"/> replaces the preset's
    /// accent — setting either turns the preset's own off, so a brand color wins whichever mode it
    /// was set for — and <see cref="AdditionalCss"/> comes after the preset and can reach the rest.
    /// </para>
    /// </remarks>
    public ReDocDarkThemePreset DarkThemePreset { get; set; } = ReDocDarkThemePreset.None;

    /// <summary>
    /// Where the document picker sits. Defaults to <see cref="ReDocToolbarPlacement.TopRight"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ReDocToolbarPlacement.Sidebar"/> is the one to reach for once there are more
    /// documents than a pill at the top right can show, or once their names are long enough to be
    /// cut off in it.
    /// </remarks>
    public ReDocToolbarPlacement PickerPlacement { get; set; } = ReDocToolbarPlacement.TopRight;

    /// <summary>
    /// Whether the picker is a searchable list rather than a plain dropdown. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Worth turning on past a handful of documents, and left off below that: a native
    /// <c>&lt;select&gt;</c> is the better control when the whole list fits on screen, not least
    /// because a phone renders it with its own picker.
    /// </para>
    /// <para>
    /// Typing filters on a document's <see cref="ReDocDocument.DisplayName"/> <em>and</em> its
    /// <see cref="ReDocDocument.Group"/>, so a group name brings up everything under it. A
    /// <see cref="ReDocDocumentsOptionsBase.Links"/> entry has no group and filters on its label.
    /// </para>
    /// </remarks>
    public bool SearchablePicker { get; set; }

    /// <summary>
    /// Where the <see cref="ToolbarItems"/> links sit. Defaults to
    /// <see cref="ReDocToolbarPlacement.TopRight"/>, and it is independent of
    /// <see cref="PickerPlacement"/>.
    /// </summary>
    /// <remarks>
    /// Matching <see cref="PickerPlacement"/> puts both in one strip, with the links first; giving
    /// them different placements gives each a strip of its own — the picker in the sidebar with
    /// the navigation and the links left in a corner, say.
    /// </remarks>
    public ReDocToolbarPlacement ToolbarItemsPlacement { get; set; } =
        ReDocToolbarPlacement.TopRight;

    /// <summary>
    /// CSS appended to the page's stylesheet, after this package's own rules — including the
    /// accent set by <see cref="PrimaryColor"/> and anything <see cref="DarkThemePreset"/> paints, so
    /// rules here win at equal specificity. ReDoc 3 is themed through CSS custom properties, so
    /// overriding e.g. <c>--color-primary-base</c> under <c>html.dark</c> or <c>:root</c> is how
    /// anything else is themed.
    /// </summary>
    public string? AdditionalCss { get; set; }

    /// <summary>
    /// A <c>Content-Security-Policy</c> to send with each page, or <see langword="null"/>, the
    /// default, to send none. <see cref="UseDefaultContentSecurityPolicy"/> sets the policy these
    /// pages are known to work under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the HTML responses carry it; a policy on a stylesheet or a script does nothing, and
    /// the one on the page already governs what that page may load.
    /// </para>
    /// <para>
    /// A policy your own middleware has already set is left alone — yours is the whole app's and
    /// knows about more than these pages. So this is for the case where the documentation pages
    /// are the only ones with a policy, not a way to override one.
    /// </para>
    /// </remarks>
    public string? ContentSecurityPolicy
    {
        get => contentSecurityPolicy;
        set => contentSecurityPolicy = ValidateHeaderValue(value, nameof(ContentSecurityPolicy));
    }

    /// <summary>
    /// Serves the pages under <see cref="DefaultContentSecurityPolicy"/>. Start here and adjust
    /// <see cref="ContentSecurityPolicy"/> if your spec lives on another origin, or if you'd
    /// rather the Redocly badge weren't fetched.
    /// </summary>
    public void UseDefaultContentSecurityPolicy() =>
        ContentSecurityPolicy = DefaultContentSecurityPolicy;

    /// <summary>
    /// Whether <c>/{RoutePrefix}</c> sends a returning viewer back to the document they were last
    /// reading, instead of always to the first one. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This costs a cookie — <c>hlibz.redoc.document</c>, holding a document name, set on each
    /// page — which is why it is off unless you ask for it: a documentation page that sets no
    /// cookies at all is worth more to some people than this is. It is <c>HttpOnly</c>,
    /// <c>SameSite=Lax</c>, <c>Secure</c> over HTTPS, scoped to the route prefix, and it expires
    /// after a year.
    /// </para>
    /// <para>
    /// Only a listed document is remembered. An <see cref="ReDocDocument.Unlisted"/> one is
    /// usually the gated one, and sending a viewer whose access has lapsed to a page that answers
    /// 401 is worse than sending them to the public one.
    /// </para>
    /// </remarks>
    public bool RememberLastDocument { get; set; }

    /// <summary>
    /// Links shown as buttons in this order. Empty by default; <see cref="AddToolbarItem"/> is the
    /// convenient way to fill it, and <see cref="ToolbarItemsPlacement"/> decides where they go.
    /// </summary>
    public IList<ReDocToolbarItem> ToolbarItems { get; } = [];

    /// <summary>
    /// Adds a link to the toolbar — a Postman collection, a changelog, the dev portal, a console
    /// rendered by something else.
    /// </summary>
    /// <param name="label">Text on the button.</param>
    /// <param name="url">
    /// Absolute, root-relative, or <c>~/</c>-relative to the app base. See
    /// <see cref="ReDocToolbarItem"/>.
    /// </param>
    /// <param name="newTab">Whether to open in a new tab.</param>
    /// <returns>These options, for chaining.</returns>
    public ReDocDocumentsOptions AddToolbarItem(string label, string url, bool newTab = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        ToolbarItems.Add(new ReDocToolbarItem(label, url, newTab));
        return this;
    }

    /// <summary>
    /// Whether the page carries print styles. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Far less is needed here than on the ReDoc 2 adapters: ReDoc 3 has <c>@media print</c> rules
    /// of its own that hide its sidebar and toolbar and give the panels the full width. What it
    /// doesn't do is color — a page printed while in dark mode comes out as near-white text on
    /// white paper, because browsers drop background colors when printing but keep text colors.
    /// So this hides the toolbar this package adds, and puts ReDoc's light palette back for the
    /// printed copy.
    /// </remarks>
    public bool PrintStyles { get; set; } = true;

    /// <summary>
    /// Whether to hide ReDoc's "Download OpenAPI description" button. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool HideDownloadButton { get; set; }

    /// <summary>
    /// Whether to list required properties before optional ones in schemas. Defaults to
    /// <see langword="false"/>, which is ReDoc's own order.
    /// </summary>
    public bool RequiredPropsFirst { get; set; }

    /// <summary>
    /// Whether ReDoc may report anonymous usage telemetry. Defaults to <see langword="false"/>:
    /// a documentation page served from someone's own app shouldn't call out to a third party
    /// unless they asked for it.
    /// </summary>
    public bool EnableTelemetry { get; set; }

    /// <summary>
    /// Extra ReDoc configuration as a JSON object, merged into the generated config after this
    /// package's own settings, e.g. <c>"""{"showExtensions": true}"""</c>. An escape hatch for
    /// ReDoc options this package doesn't surface; it is parsed at startup and a value that isn't
    /// a JSON object throws there rather than producing a blank page.
    /// </summary>
    public string? AdditionalConfigJson
    {
        get => additionalConfigJson;
        set
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value))
            {
                Validate(value);
            }

            additionalConfigJson = value;
        }
    }

    /// <summary>
    /// A value that is safe to put in a response header, which means no line break: the policy is
    /// the app author's own string, but a stray newline in it is header injection rather than a
    /// broken page.
    /// </summary>
    private static string? ValidateHeaderValue(string? value, string propertyName)
    {
        if (value is null || value.Trim().Length == 0)
        {
            return null;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException(
                    $"{propertyName} is sent as a response header, so it cannot contain a line "
                    + "break or any other control character.",
                    propertyName);
            }
        }

        return value.Trim();
    }

    /// <summary>
    /// A color value that is safe to write into a declaration. Not a parser: ReDoc's variables
    /// take any CSS color — hex, <c>rgb()</c>, <c>oklch()</c>, a named color, even a
    /// <c>var()</c> — so this rejects only what would escape the declaration it sits in.
    /// </summary>
    private static string? ValidateColor(string? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        foreach (char character in trimmed)
        {
            if (character is ';' or '{' or '}' or '<' or '>' or '\\'
                || char.IsControl(character))
            {
                throw new ArgumentException(
                    $"{propertyName} is written into the page's stylesheet as a CSS color, so it "
                    + $"cannot contain '{character}'. Pass a plain color value such as "
                    + "\"#38bdf8\", \"rgb(56 189 248)\" or \"dodgerblue\"; for anything more, use "
                    + "AdditionalCss.",
                    propertyName);
            }
        }

        if (trimmed.Contains("/*", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{propertyName} cannot contain a CSS comment.", propertyName);
        }

        return trimmed;
    }

    private static void Validate(string json)
    {
        JsonValueKind kind;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            kind = document.RootElement.ValueKind;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"AdditionalConfigJson is not valid JSON: {exception.Message}",
                nameof(AdditionalConfigJson),
                exception);
        }

        if (kind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"AdditionalConfigJson must be a JSON object, e.g. {{\"showExtensions\": true}}, "
                + $"but it is a JSON {kind.ToString().ToLowerInvariant()}.",
                nameof(AdditionalConfigJson));
        }
    }
}
