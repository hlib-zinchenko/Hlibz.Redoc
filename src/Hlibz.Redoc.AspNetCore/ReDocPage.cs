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

    /// <summary>The <c>localStorage</c> key ReDoc 3 keeps the viewer's color mode under.</summary>
    private const string ColorModeKey = "redoc.colorMode";

    public static string Html(string title, ReDocDocumentsOptions options, bool withPicker)
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

        page.Append("</body>\n");
        page.Append("</html>\n");
        return page.ToString();
    }

    /// <summary>
    /// Sets the color-mode class before the first paint, the same way ReDoc 3 does it inline.
    /// Served as a file so no inline script is needed.
    /// </summary>
    public static string ColorModeScript(ReDocColorMode fallback)
    {
        string fallbackValue = fallback switch
        {
            ReDocColorMode.Light => "'light'",
            ReDocColorMode.Dark => "'dark'",
            _ => "(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches "
                + "? 'dark' : 'light')",
        };

        return $$"""
            (function () {
              var mode;
              try { mode = localStorage.getItem('{{ColorModeKey}}'); } catch (e) { }
              if (mode !== 'light' && mode !== 'dark') { mode = {{fallbackValue}}; }
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
        if (withPicker)
        {
            css.Append(ReDocDocumentPicker.Css).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(options.AdditionalCss))
        {
            css.Append('\n').Append(options.AdditionalCss.Trim()).Append('\n');
        }

        return css.ToString();
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
