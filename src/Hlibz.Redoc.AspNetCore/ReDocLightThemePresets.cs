using System.Text;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The palettes behind <see cref="ReDocLightThemePreset"/>, as the CSS they are emitted as.
/// </summary>
/// <remarks>
/// <para>
/// Light mode is not dark mode's mirror in every respect, which is why this is not a reversed
/// <see cref="ReDocDarkThemePresets"/> table. Read off ReDoc 3.0.1's own stylesheet: the ramp runs
/// lightest-first, but the page background is <c>--color-white</c>, not a ramp slot, while panels
/// are slot 1, the primary border slot 3, and text slots 11 (primary), 8 (secondary), 7
/// (descriptions) and 6 (helper). Dark mode redefines <c>--color-white</c> as black, so it is set
/// here for light mode only.
/// </para>
/// <para>
/// <c>:root:root:not(.dark)</c> outweighs ReDoc's own <c>:root</c> palette, which its runtime
/// <c>&lt;style&gt;</c> declares after this stylesheet, and can never apply in dark mode, so a
/// dark preset and a light one sit side by side without either knowing about the other.
/// </para>
/// </remarks>
internal static class ReDocLightThemePresets
{
    /// <summary>
    /// The CSS for a preset, or <see langword="null"/> for
    /// <see cref="ReDocLightThemePreset.None"/>.
    /// </summary>
    /// <param name="preset">The preset to render.</param>
    /// <param name="withAccent">
    /// Whether to emit the preset's accent: <see langword="false"/> when the caller set
    /// <see cref="ReDocDocumentsOptions.PrimaryColor"/>, whose <c>:root:root</c> rule this
    /// preset's selector would otherwise outweigh in light mode.
    /// </param>
    public static string? Css(ReDocLightThemePreset preset, bool withAccent)
    {
        if (PaletteOf(preset) is not { } palette)
        {
            return null;
        }

        StringBuilder css = new();
        css.Append("\n:root:root:not(.dark) {\n");
        css.Append($"  --color-white: {palette.Page};\n");
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

    private static Palette? PaletteOf(ReDocLightThemePreset preset) => preset switch
    {
        // Nord's bright ambiance as Nord documents it: nord6 is the background, nord5 and nord4
        // the elevated surfaces and borders, nord3-0 the text, darkest last. Slots 4-7 bridge the
        // gap between the two groups with the same greys the dark Nord preset uses for it, and
        // nord10 is the accent Nord gives for a light background.
        ReDocLightThemePreset.Nord => new Palette(
            Page: "#eceff4",
            [
                "#e5e9f0", "#dde3ec", "#d8dee9", "#c0c9d9", "#8e9ab3", "#7b88a1",
                "#616e88", "#4c566a", "#434c5e", "#3b4252", "#2e3440",
            ],
            Accent: "#5e81ac"),

        // GitHub's light theme by its Primer names: canvas.default for the page, then the
        // neutral scale gray-0 to gray-9 in order, fg.default as the primary text and accent.fg
        // as the accent.
        ReDocLightThemePreset.GitHubLight => new Palette(
            Page: "#ffffff",
            [
                "#f6f8fa", "#eaeef2", "#d0d7de", "#afb8c1", "#8c959f", "#6e7781",
                "#57606a", "#424a53", "#32383f", "#24292f", "#1f2328",
            ],
            Accent: "#0969da"),

        _ => null,
    };

    /// <summary>
    /// One preset: the page background, eleven greys lightest-first, and the accent.
    /// </summary>
    private sealed record Palette(string Page, string[] Ramp, string Accent);
}
