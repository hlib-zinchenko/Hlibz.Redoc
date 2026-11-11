namespace Hlibz.Redoc.Extensions;

/// <summary>
/// A named palette for ReDoc's light mode, the counterpart of <see cref="ReDocDarkThemePreset"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each preset re-points ReDoc 3's eleven-step grey ramp, the page background and the accent.
/// Only palettes that publish a light variant of their own are here: Nord's bright ambiance and
/// GitHub's light theme. There is no light Midnight, because Midnight is this package's own dark
/// palette and a light one invented to match would be nobody's theme.
/// </para>
/// <para>
/// Pairs naturally with the dark preset of the same name, so a page follows one palette in both
/// modes — <c>Nord</c> with <see cref="ReDocDarkThemePreset.Nord"/>, <c>GitHubLight</c> with
/// <see cref="ReDocDarkThemePreset.GitHubDark"/> — but the two are independent settings.
/// </para>
/// </remarks>
public enum ReDocLightThemePreset
{
    /// <summary>ReDoc's own light palette. The default.</summary>
    None,

    /// <summary>
    /// Nord's bright ambiance: Snow Storm surfaces, Polar Night text, Nord's blue (nord10) as the
    /// accent.
    /// </summary>
    Nord,

    /// <summary>
    /// GitHub's light theme: white canvas, its grey scale for panels, borders and text, and its
    /// blue accent.
    /// </summary>
    GitHubLight,
}
