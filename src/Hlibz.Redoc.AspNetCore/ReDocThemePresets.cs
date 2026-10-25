using System.Text;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The palettes behind <see cref="ReDocThemePreset"/>, as the CSS they are emitted as.
/// </summary>
/// <remarks>
/// <para>
/// A preset is eleven greys and an accent, and that is not a simplification: ReDoc 3's dark mode is
/// its grey ramp reversed, and every semantic token it renders from — the page background, the
/// panels, both border weights, primary and secondary text, code blocks — resolves to
/// <c>var(--color-warm-grey-N)</c>. Measured against 3.0.1 by reading the computed values in both
/// modes: the two ramps are exact mirrors of each other, index 2 is the page background, 3 the
/// panels, 5 the primary border, 8 secondary text and 11 primary text. So re-pointing those eleven
/// values is a whole theme, which is why presets are worth having here and were not on ReDoc 2,
/// where a theme could only reach the keys the theme object exposed.
/// </para>
/// <para>
/// The doubled <c>:root:root.dark</c> selector is the same trick, and for the same reason, as
/// <c>ReDocPage.AppendBrandCss</c>: ReDoc defines its palette from a <c>&lt;style&gt;</c>
/// styled-components injects at runtime, which lands after this stylesheet's <c>&lt;link&gt;</c>,
/// so a plain <c>:root.dark</c> would lose on source order.
/// </para>
/// </remarks>
internal static class ReDocThemePresets
{
    /// <summary>
    /// The CSS for a preset, or <see langword="null"/> for <see cref="ReDocThemePreset.None"/>.
    /// </summary>
    /// <param name="preset">The preset to render.</param>
    /// <param name="withAccent">
    /// Whether to emit the preset's accent. <see langword="false"/> when the caller named an accent
    /// of their own, which must win: theirs may be a plain <c>:root:root</c> rule, which a preset's
    /// <c>:root:root.dark</c> would otherwise outweigh in dark mode however it was ordered.
    /// </param>
    public static string? Css(ReDocThemePreset preset, bool withAccent)
    {
        if (PaletteOf(preset) is not { } palette)
        {
            return null;
        }

        StringBuilder css = new();
        css.Append("\n:root:root.dark {\n");
        for (int index = 0; index < palette.Ramp.Length; index++)
        {
            css.Append($"  --color-warm-grey-{index + 1}: {palette.Ramp[index]};\n");
        }

        if (withAccent)
        {
            css.Append($"  --color-primary-base: {palette.Accent};\n");
            css.Append($"  --link-color-primary: {palette.Accent};\n");
        }

        css.Append("}\n");
        return css.ToString();
    }

    /// <summary>
    /// The accent a printed copy of this preset should use, or <see langword="null"/> when there is
    /// nothing to change — the accents here were picked to carry on a near-black page, and the
    /// printed copy is on white paper.
    /// </summary>
    public static string? PrintAccent(ReDocThemePreset preset) => PaletteOf(preset)?.PrintAccent;

    private static Palette? PaletteOf(ReDocThemePreset preset) => preset switch
    {
        // This repo's own ReDoc 2 dark theme, as a ramp: zinc surfaces, a sky accent. The values
        // are the ones ReDocDarkThemeDefaults paints with — #09090b behind the page, #18181b behind
        // nested schemas, #3f3f46 on the scrollbar, #d4d4d8 and #a1a1aa on the section captions,
        // and DefaultPrimaryLight/DefaultPrimaryDark as the two accents.
        ReDocThemePreset.Midnight => new Palette(
            [
                "#050507", "#09090b", "#18181b", "#27272a", "#3f3f46", "#52525b",
                "#71717a", "#a1a1aa", "#d4d4d8", "#e4e4e7", "#fafafa",
            ],
            Accent: "#7dd3fc",
            PrintAccent: "#0284c7"),

        // Nord, in its own order: nord0-3 (Polar Night) for the surfaces and borders, nord4-6
        // (Snow Storm) for the text, nord8 (Frost) as the accent and nord10 for paper. Index 1 is
        // below nord0, and indices 7-8 bridge Nord's deliberate gap between nord3 and nord4.
        ReDocThemePreset.Nord => new Palette(
            [
                "#242933", "#2e3440", "#3b4252", "#434c5e", "#4c566a", "#616e88",
                "#8e9ab3", "#c0c9d9", "#d8dee9", "#e5e9f0", "#eceff4",
            ],
            Accent: "#88c0d0",
            PrintAccent: "#5e81ac"),

        // GitHub's dark theme by its Primer names: canvas inset/default/subtle, border muted and
        // default, then fg subtle/muted/default, with accent.fg as the accent and the light
        // theme's accent.fg for paper.
        ReDocThemePreset.GitHubDark => new Palette(
            [
                "#010409", "#0d1117", "#161b22", "#21262d", "#30363d", "#484f58",
                "#6e7681", "#8b949e", "#b1bac4", "#c9d1d9", "#e6edf3",
            ],
            Accent: "#2f81f7",
            PrintAccent: "#0969da"),

        _ => null,
    };

    /// <summary>
    /// One preset: eleven greys darkest-first, the dark-mode accent, and the accent for print.
    /// </summary>
    private sealed record Palette(string[] Ramp, string Accent, string PrintAccent);
}
