using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// The font stacks behind <see cref="DarkThemeOptions.UseSystemFonts"/>, the ReDoc
/// <c>typography</c> config that carries the font overrides, and the index-page rewrite that drops
/// ReDoc's Google Fonts request.
/// </summary>
/// <remarks>
/// <para>
/// Redoc.AspNetCore's stock <c>index.html</c> links Montserrat and Roboto from
/// <c>fonts.googleapis.com</c>, and that <c>&lt;link&gt;</c> sits above <c>%(HeadContent)</c> in
/// the template — by the time a script injected through <c>HeadContent</c> runs, the browser has
/// already started the request. Not making it at all therefore means rewriting the page, which is
/// what <see cref="WithoutWebFontLinks"/> does to whichever <c>IndexStream</c> is in effect.
/// </para>
/// <para>
/// Font families are also the first theme setting whose value is free text rather than a color,
/// which runs into how that page carries the config: <c>JSON.parse('%(ConfigObject)')</c>, a
/// JavaScript string literal that keeps no backslashes. <c>JsonSerializer</c>'s default encoder
/// writes a quote as a unicode escape, which that literal turns back into a bare quote before
/// <c>JSON.parse</c> sees it, taking the page down with a syntax error. So font stacks are
/// held to characters that survive the round trip — see <see cref="Resolve"/>.
/// </para>
/// </remarks>
internal static partial class ReDocFonts
{
    /// <summary>What a font stack may contain besides ASCII letters and digits.</summary>
    private const string AllowedPunctuation = "-_. ,";

    /// <summary>The OS UI font, with the usual fallbacks for hosts that don't report one.</summary>
    public const string SystemSansSerif =
        "system-ui, -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial, Noto Sans, sans-serif";

    /// <summary>The OS monospace font, used for code samples and schema types.</summary>
    public const string SystemMonospace =
        "ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, Liberation Mono, monospace";

    /// <summary>
    /// The configured font stacks, or <see langword="null"/> entries where ReDoc's own defaults
    /// should stand. Quotes are dropped — optional in CSS for a family name, and fatal to the page
    /// that carries the config — and anything else the page can't carry throws instead.
    /// </summary>
    public static FontStacks Resolve(DarkThemeOptions theme) =>
        new(
            Prepare(theme.FontFamily, nameof(DarkThemeOptions.FontFamily)),
            Prepare(theme.HeadingsFontFamily, nameof(DarkThemeOptions.HeadingsFontFamily)),
            Prepare(theme.CodeFontFamily, nameof(DarkThemeOptions.CodeFontFamily)));

    /// <summary>
    /// The <c>typography</c> entries for the configured font families, or <see langword="null"/>
    /// when none is set. Merged into the dark theme config by
    /// <see cref="ReDocDarkThemeDefaults.BuildConfigValue"/>.
    /// </summary>
    public static Dictionary<string, object>? BuildTypographyConfig(FontStacks fonts)
    {
        if (fonts.IsEmpty)
        {
            return null;
        }

        Dictionary<string, object> typography = new();
        if (fonts.Body is not null)
        {
            typography["fontFamily"] = fonts.Body;
        }

        if (fonts.Headings is not null)
        {
            typography["headings"] =
                new Dictionary<string, object> { ["fontFamily"] = fonts.Headings };
        }

        if (fonts.Code is not null)
        {
            typography["code"] = new Dictionary<string, object> { ["fontFamily"] = fonts.Code };
        }

        return typography;
    }

    /// <summary>
    /// The font overrides as a JSON ReDoc theme object, or the literal <c>null</c> when none is
    /// set. The theme selector drops the dark theme config for its light theme, so the fonts have
    /// to be handed to ReDoc separately to survive a switch to light.
    /// </summary>
    /// <remarks>
    /// Written with <see cref="Utf8JsonWriter"/>: its default encoder escapes <c>&lt;</c>,
    /// <c>&gt;</c> and <c>&amp;</c>, so the result is safe to embed in the selector's
    /// <c>&lt;script&gt;</c> — where, unlike ReDoc's own page, the JSON is the script rather than a
    /// string literal inside it — and it needs no reflection.
    /// </remarks>
    public static string BuildThemeConfigJson(FontStacks fonts)
    {
        if (fonts.IsEmpty)
        {
            return "null";
        }

        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("typography");
            if (fonts.Body is not null)
            {
                writer.WriteString("fontFamily", fonts.Body);
            }

            if (fonts.Headings is not null)
            {
                writer.WriteStartObject("headings");
                writer.WriteString("fontFamily", fonts.Headings);
                writer.WriteEndObject();
            }

            if (fonts.Code is not null)
            {
                writer.WriteStartObject("code");
                writer.WriteString("fontFamily", fonts.Code);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Wraps an <c>IndexStream</c> so the served page carries no <c>&lt;link&gt;</c> to Google's
    /// font hosts. Wrapping rather than replacing keeps a caller's own template working.
    /// </summary>
    public static Func<Stream> WithoutWebFontLinks(Func<Stream> indexStream)
    {
        return () =>
        {
            string index;
            using (Stream source = indexStream())
            {
                using StreamReader reader = new(source, Encoding.UTF8);
                index = reader.ReadToEnd();
            }

            string stripped = WebFontLink().Replace(index, string.Empty);
            return new MemoryStream(Encoding.UTF8.GetBytes(stripped));
        };
    }

    private static string? Prepare(string? family, string property)
    {
        if (family is null)
        {
            return null;
        }

        string stripped = family.Replace("\"", string.Empty).Replace("'", string.Empty).Trim();
        foreach (char character in stripped)
        {
            if (char.IsAsciiLetterOrDigit(character) || AllowedPunctuation.Contains(character))
            {
                continue;
            }

            throw new ArgumentException(
                $"Font family '{family}' can't be used: ReDoc's page carries the theme config "
                + $"inside a JavaScript string literal, which '{character}' does not survive — it "
                + "would break the whole page rather than just the font. Use a plain font stack of "
                + "ASCII names, e.g. 'Inter, system-ui, sans-serif'. Quotes are fine; they are "
                + "dropped, and CSS doesn't need them for a family name.",
                property);
        }

        return stripped.Length == 0 ? null : stripped;
    }

    // Both hosts, and any number of tags: a custom template may also preconnect to fonts.gstatic.
    [GeneratedRegex(
        """[ \t]*<link\b[^>]*\bfonts\.(?:googleapis|gstatic)\.com[^>]*>[ \t]*\r?\n?""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebFontLink();
}

/// <summary>
/// The three font stacks a <see cref="DarkThemeOptions"/> configures, normalized, with
/// <see langword="null"/> wherever ReDoc's own default should stand.
/// </summary>
internal sealed record FontStacks(string? Body, string? Headings, string? Code)
{
    public bool IsEmpty => Body is null && Headings is null && Code is null;
}
