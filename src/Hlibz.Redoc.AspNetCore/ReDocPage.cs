using System.Net;
using System.Text;
using System.Text.Json;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The page this package serves, and the files it references.
/// </summary>
/// <remarks>
/// <para>
/// Nothing on the page is inline: the ReDoc config, the color-mode bootstrap, the picker and all
/// CSS are served as their own files. That is what lets a strict
/// <c>Content-Security-Policy</c> cover these pages without a nonce — which is also why the nonce
/// idea was dropped, since ReDoc renders through styled-components and needs
/// <c>style-src 'unsafe-inline'</c> no matter what we do.
/// </para>
/// <para>
/// The color mode is stamped on <c>&lt;html&gt;</c> server-side where it is known. ReDoc 3 does
/// the same thing with an inline script of its own, which a strict policy blocks — so without
/// this the page would render light for a moment before React caught up.
/// </para>
/// </remarks>
internal static class ReDocPage
{
    public const string InitFileName = "redoc-init.js";
    public const string StyleFileName = "redoc-page.css";
    public const string PickerFileName = "redoc-picker.js";
    public const string ColorModeFileName = "redoc-colormode.js";
    public const string ToolbarFileName = "redoc-toolbar.js";

    /// <summary>The <c>localStorage</c> key ReDoc 3 keeps the viewer's color mode under.</summary>
    private const string ColorModeKey = "redoc.colorMode";

    public static string Html(
        string title, ReDocDocumentsOptions options, bool withPicker, bool withToolbarItems)
    {
        string? mode = options.DefaultColorMode switch
        {
            ReDocColorMode.Light => "light",
            ReDocColorMode.Dark => "dark",
            _ => null,
        };

        StringBuilder page = new();
        page.Append("<!DOCTYPE html>\n");

        // Stamped here when it's known, so the first byte already carries it; redoc-colormode.js
        // fills it in for System and corrects it to whatever the viewer chose last time.
        page.Append(mode is null
            ? "<html lang=\"en\">\n"
            : $"<html lang=\"en\" class=\"{mode}\" data-redoc-color-mode=\"{mode}\">\n");

        page.Append("<head>\n");
        page.Append("<meta charset=\"utf-8\">\n");
        page.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        page.Append(mode is null
            ? "<meta name=\"color-scheme\" content=\"light dark\">\n"
            : $"<meta name=\"color-scheme\" content=\"{mode}\">\n");
        page.Append($"<title>{title}</title>\n");

        if (options.Favicon is not null)
        {
            page.Append($"<link rel=\"icon\" href=\"{WebUtility.HtmlEncode(options.Favicon)}\">\n");
        }

        page.Append($"<link rel=\"stylesheet\" href=\"{StyleFileName}\">\n");

        // Before anything paints, and before the bundle: blocking on purpose.
        page.Append($"<script src=\"{ColorModeFileName}\"></script>\n");
        page.Append("</head>\n");
        page.Append("<body>\n");
        page.Append("<div id=\"redoc-container\"></div>\n");

        // A module script is deferred, so the container exists by the time it runs.
        page.Append($"<script type=\"module\" src=\"{InitFileName}\"></script>\n");
        if (withPicker)
        {
            page.Append($"<script src=\"{PickerFileName}\"></script>\n");
        }

        if (withToolbarItems)
        {
            page.Append($"<script src=\"{ToolbarFileName}\"></script>\n");
        }

        page.Append("</body>\n");
        page.Append("</html>\n");
        return page.ToString();
    }

    /// <summary>
    /// Sets the color-mode class before the first paint, the same way ReDoc 3 does it inline, and
    /// records a configured default under ReDoc's own storage key. Served as a file so no inline
    /// script is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting the class is not enough on its own, which is only visible once you try it: ReDoc's
    /// store resolves the mode as <c>localStorage['redoc.colorMode']</c> first and its own cookie
    /// second, falling back to <c>prefers-color-scheme</c>, and then writes the class itself. With
    /// that key empty — a first-time viewer — it would resolve the mode from the viewer's OS and
    /// overwrite whatever the server stamped, so a configured
    /// <see cref="ReDocColorMode.Light"/> or <see cref="ReDocColorMode.Dark"/> did nothing at all
    /// for the people it is meant for. Writing the key is what makes the setting hold, and it is
    /// ReDoc's own key, read by ReDoc's own code.
    /// </para>
    /// <para>
    /// Only a configured Light or Dark is written. <see cref="ReDocColorMode.System"/> leaves the
    /// key alone, so the page keeps following the viewer's OS rather than being pinned to whatever
    /// it happened to be on their first visit. A viewer who has chosen a mode already always wins:
    /// their value is in that key and nothing here overwrites it.
    /// </para>
    /// </remarks>
    public static string ColorModeScript(ReDocColorMode fallback)
    {
        string fallbackValue = fallback switch
        {
            ReDocColorMode.Light => "'light'",
            ReDocColorMode.Dark => "'dark'",
            _ => "(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches "
                + "? 'dark' : 'light')",
        };

        // Nothing is persisted for System: the point of System is to keep following the OS.
        string persist = fallback == ReDocColorMode.System
            ? string.Empty
            : $"\n    try {{ localStorage.setItem('{ColorModeKey}', mode); }} catch (e) {{ }}";

        return $$"""
            (function () {
              var mode;
              try { mode = localStorage.getItem('{{ColorModeKey}}'); } catch (e) { }
              if (mode !== 'light' && mode !== 'dark') {
                mode = {{fallbackValue}};{{persist}}
              }
              var root = document.documentElement;
              root.classList.remove('light', 'dark');
              root.classList.add(mode);
              root.setAttribute('data-redoc-color-mode', mode);
            })();

            """;
    }

    /// <summary>The ES module that starts ReDoc: the only place the config appears.</summary>
    /// <remarks>
    /// The import specifier is resolved against this file's own URL, which is the page's
    /// directory, so the same relative href works from every page whatever its route depth.
    /// </remarks>
    public static string InitScript(string bundleHref, string specUrl, string configJson) =>
        $$"""
        import { init } from {{JsonString(bundleHref)}};

        init(
          {{JsonString(specUrl)}},
          {{configJson}},
          document.getElementById('redoc-container'));

        """;

    /// <summary>
    /// A string as a JSON literal, which is also a valid JavaScript one. Written rather than
    /// serialized so this assembly needs no reflection-based JSON.
    /// </summary>
    private static string JsonString(string value)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStringValue(value);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// The toolbar's links, as the script that appends them to the shared container.
    /// </summary>
    /// <remarks>
    /// Written with <see cref="Utf8JsonWriter"/> rather than serialized, for the same reason the
    /// config is: a slim or AOT host has reflection-based serialization off. The anchors are built
    /// through <c>textContent</c> and <c>setAttribute</c>, never <c>innerHTML</c>, so a label with
    /// markup in it is text on the page rather than markup in it.
    /// </remarks>
    public static string ToolbarScript(
        IEnumerable<ReDocToolbarItem> items, string currentRoute, ReDocToolbarSpot spot)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartArray();
            foreach (ReDocToolbarItem item in items)
            {
                writer.WriteStartObject();
                writer.WriteString("label", item.Label);
                writer.WriteString("href", ResolveUrl(item.Url, currentRoute));
                if (item.NewTab)
                {
                    writer.WriteBoolean("newTab", true);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        string json = Encoding.UTF8.GetString(buffer.ToArray());

        return $$"""
            (function () {
              var items = {{json}};

            {{ReDocToolbar.ScriptFunctionFor(spot)}}

              function render() {
                var toolbar = hlibzToolbar();
                items.forEach(function (item) {
                  var link = document.createElement('a');
                  link.className = 'hlibz-toolbar-item';
                  link.href = item.href;
                  link.textContent = item.label;
                  if (item.newTab) {
                    link.target = '_blank';
                    link.rel = 'noopener';
                  }
                  toolbar.appendChild(link);
                });
              }

              if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', render);
              } else {
                render();
              }
            })();

            """;
    }

    /// <summary>
    /// A link URL as the page should carry it: <c>~/</c> resolved relative to this page's own
    /// route depth, anything else left alone. The same rule <see cref="ReDocLink"/> follows, so
    /// an href needs no knowledge of the <c>PathBase</c> and survives a path-rewriting proxy.
    /// </summary>
    private static string ResolveUrl(string url, string currentRoute) =>
        url.StartsWith("~/", StringComparison.Ordinal)
            ? ReDocRelativePath.Between(currentRoute, url[2..])
            : url;

    /// <summary>The page's stylesheet: our own rules, then whatever the caller added.</summary>
    public static string Css(ReDocDocumentsOptions options, bool withPicker)
    {
        const string baseCss = """
            html, body { margin: 0; padding: 0; }

            /* ReDoc 3 themes itself through CSS custom properties, so this package's controls
               borrow its variables rather than carrying a palette of their own: they then follow
               the active color mode, and any brand override, for free. The fallbacks are only for
               the moment before ReDoc's own styles are in the document. */
            html.dark {
              --hlibz-control-bg: var(--layer-color, rgba(24, 24, 27, 0.92));
              --hlibz-control-border: var(--border-color-primary, #3f3f46);
              --hlibz-control-fg: var(--text-color-secondary, #a1a1aa);
              --hlibz-control-fg-hover: var(--text-color-primary, #fafafa);
              --hlibz-control-accent: var(--color-primary-base, #0284c7);
              --hlibz-control-accent-fg: var(--bg-color, #09090b);
            }

            """;

        StringBuilder css = new();
        css.Append(baseCss);
        css.Append(ReDocToolbar.Css);
        bool withItems = options.ToolbarItems.Count > 0;
        if (withPicker)
        {
            css.Append(ReDocDocumentPicker.Css).Append('\n');
            if (options.SearchablePicker)
            {
                css.Append(ReDocDocumentPicker.SearchCss).Append('\n');
            }
        }

        if (withItems)
        {
            css.Append(ReDocToolbar.ItemCss).Append('\n');
        }

        // Only the placements something is actually in, so the stylesheet says what the page does.
        if (PlacedAt(options, ReDocToolbarPlacement.BottomRight, withPicker, withItems))
        {
            css.Append(ReDocToolbar.BottomRightCss).Append('\n');
        }

        // Only when one horizontal strip holds both, which is the only time their order shows.
        if (withPicker
            && withItems
            && options.PickerPlacement == options.ToolbarItemsPlacement
            && options.PickerPlacement != ReDocToolbarPlacement.Sidebar)
        {
            css.Append(ReDocToolbar.PickerAfterItemsCss).Append('\n');
        }

        // Last of the four, so its own order rule outweighs the one above when a page has both a
        // sidebar strip and a corner strip holding the picker and the links together.
        if (PlacedAt(options, ReDocToolbarPlacement.Sidebar, withPicker, withItems))
        {
            css.Append(ReDocToolbar.SidebarCss).Append('\n');
        }

        // Before the accent, so an explicitly set PrimaryColor is the last word on the accent, and
        // before AdditionalCss for the same reason.
        if (ReDocThemePresets.Css(options.ThemePreset, withAccent: HasPresetAccent(options))
            is { } preset)
        {
            css.Append(preset);
        }

        AppendBrandCss(css, options);
        if (options.PrintStyles)
        {
            AppendPrintCss(css, options);
        }

        if (!string.IsNullOrWhiteSpace(options.AdditionalCss))
        {
            css.Append('\n').Append(options.AdditionalCss.Trim()).Append('\n');
        }

        return css.ToString();
    }

    /// <summary>
    /// The accent, as the two variables ReDoc's accent actually comes from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The doubled selectors are not a typo, and they are the whole difficulty of this feature.
    /// ReDoc defines its palette on <c>:root</c> and <c>:root.dark</c> from a
    /// <c>&lt;style&gt;</c> that styled-components injects into the head at runtime — which is
    /// after this stylesheet's <c>&lt;link&gt;</c>, so at equal specificity ReDoc wins on source
    /// order. <c>:root:root</c> and <c>:root:root.dark</c> each outweigh the rule they have to
    /// beat by one class, and nothing about the order matters any more.
    /// </para>
    /// <para>
    /// Dark is stated as <c>.dark</c> on the root for the same reason ReDoc does: that is the
    /// class the color mode is stamped with. This package's own controls read
    /// <c>--color-primary-base</c>, so they follow the accent with no extra rule.
    /// </para>
    /// </remarks>
    /// <summary>Whether this page has a control at a given placement at all.</summary>
    private static bool PlacedAt(
        ReDocDocumentsOptions options,
        ReDocToolbarPlacement placement,
        bool withPicker,
        bool withItems) =>
        (withPicker && options.PickerPlacement == placement)
        || (withItems && options.ToolbarItemsPlacement == placement);

    /// <summary>
    /// Core's spelling of a placement, which is the one the scripts are built from.
    /// </summary>
    public static ReDocToolbarSpot SpotOf(ReDocToolbarPlacement placement) => placement switch
    {
        ReDocToolbarPlacement.BottomRight => ReDocToolbarSpot.BottomRight,
        ReDocToolbarPlacement.Sidebar => ReDocToolbarSpot.Sidebar,
        _ => ReDocToolbarSpot.TopRight,
    };

    /// <summary>
    /// Whether the accent on the page is the preset's. Naming either color takes it over: the
    /// preset paints dark mode, so a <see cref="ReDocDocumentsOptions.PrimaryColor"/> meant for
    /// both modes would otherwise be outweighed there by a selector one class heavier.
    /// </summary>
    private static bool HasPresetAccent(ReDocDocumentsOptions options) =>
        options.PrimaryColor is null && options.DarkModePrimaryColor is null;

    private static void AppendBrandCss(StringBuilder css, ReDocDocumentsOptions options)
    {
        if (options.PrimaryColor is { } accent)
        {
            css.Append('\n')
                .Append(":root:root {\n")
                .Append($"  --color-primary-base: {accent};\n")
                .Append($"  --link-color-primary: {accent};\n")
                .Append("}\n");
        }

        if (options.DarkModePrimaryColor is { } darkAccent)
        {
            css.Append('\n')
                .Append(":root:root.dark {\n")
                .Append($"  --color-primary-base: {darkAccent};\n")
                .Append($"  --link-color-primary: {darkAccent};\n")
                .Append("}\n");
        }
    }

    /// <summary>
    /// What printing needs that ReDoc 3 doesn't already do for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ReDoc 3 ships <c>@media print</c> rules that hide its sidebar and its buttons and give the
    /// panels the full page width, so none of that is repeated here — unlike the ReDoc 2 adapters,
    /// where the print stylesheet has to do the whole job. Two things are left.
    /// </para>
    /// <para>
    /// The toolbar is this package's own and ReDoc's rules can't know about it, so it goes.
    /// </para>
    /// <para>
    /// And color: browsers omit background colors when printing but keep text colors, so a page
    /// printed while in dark mode is near-white text on white paper. ReDoc 3's dark mode turns out
    /// to be the grey ramp reversed — every semantic token reads
    /// <c>var(--color-warm-grey-N)</c> in both modes, and only those eleven values differ — so
    /// restoring the light ramp for the printed copy is the entire fix, and everything derived
    /// from it (text, borders, panels, code blocks) follows. Measured in the browser against
    /// 3.0.1; the two ramps are exact mirrors of each other.
    /// </para>
    /// </remarks>
    private static void AppendPrintCss(StringBuilder css, ReDocDocumentsOptions options)
    {
        css.Append("""

            @media print {
              .hlibz-toolbar { display: none !important; }

              :root:root.dark {
                --color-warm-grey-1: #fbfbfc;
                --color-warm-grey-2: #ededf2;
                --color-warm-grey-3: #dcdde5;
                --color-warm-grey-4: #c4c6d1;
                --color-warm-grey-5: #9b9ca8;
                --color-warm-grey-6: #6e6f7a;
                --color-warm-grey-7: #555761;
                --color-warm-grey-8: #3b3c45;
                --color-warm-grey-9: #2a2b33;
                --color-warm-grey-10: #22242b;
                --color-warm-grey-11: #1a1c21;
              }

            """);

        // Same thing for a preset's accent, which is picked for its own near-black surfaces: every
        // preset names the shade of itself that reads on paper.
        if (HasPresetAccent(options)
            && ReDocThemePresets.PrintAccent(options.ThemePreset) is { } presetAccent)
        {
            css.Append($$"""
                  :root:root.dark {
                    --color-primary-base: {{presetAccent}};
                    --link-color-primary: {{presetAccent}};
                  }

                """);
        }

        // The dark accent was chosen to carry on a near-black page; on paper the light one is the
        // right choice, and it is already known to work against a light background.
        if (options.DarkModePrimaryColor is not null && options.PrimaryColor is { } accent)
        {
            css.Append($$"""
                  :root:root.dark {
                    --color-primary-base: {{accent}};
                    --link-color-primary: {{accent}};
                  }

                """);
        }

        css.Append("}\n");
    }

    /// <summary>
    /// The ReDoc config as JSON. Written rather than serialized: a
    /// <c>Dictionary&lt;string, object&gt;</c> can't be source-generated, and this assembly claims
    /// to be AOT-compatible.
    /// </summary>
    public static string ConfigJson(ReDocDocumentsOptions options)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("disableTelemetry", !options.EnableTelemetry);
            if (options.HideDownloadButton)
            {
                writer.WriteBoolean("hideDownloadButton", true);
            }

            if (options.RequiredPropsFirst)
            {
                writer.WriteBoolean("requiredPropsFirst", true);
            }

            if (!string.IsNullOrWhiteSpace(options.AdditionalConfigJson))
            {
                // Validated as a JSON object when the property was set.
                using JsonDocument extra = JsonDocument.Parse(options.AdditionalConfigJson);
                foreach (JsonProperty property in extra.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
