namespace Hlibz.Redoc.Extensions;

/// <summary>
/// A named palette for ReDoc's dark mode, as an alternative to setting colors one at a time.
/// </summary>
/// <remarks>
/// <para>
/// Each preset re-points ReDoc 3's eleven-step grey ramp and its accent, which between them decide
/// every surface, border and piece of text on the page. Light mode is left as ReDoc ships it: all
/// three of these are dark palettes, and a "Nord light" invented here would be neither Nord nor
/// ReDoc. Pair a preset with <see cref="ReDocColorMode.Dark"/> if everyone should see it.
/// </para>
/// <para>
/// A preset is a starting point, not a lock: <see cref="ReDocDocumentsOptions.PrimaryColor"/> and
/// <see cref="ReDocDocumentsOptions.DarkModePrimaryColor"/> replace its accent, and
/// <see cref="ReDocDocumentsOptions.AdditionalCss"/> comes after it and can reach everything else.
/// </para>
/// </remarks>
public enum ReDocThemePreset
{
    /// <summary>ReDoc's own dark palette. The default.</summary>
    None,

    /// <summary>
    /// Near-black with a sky-blue accent — this repo's ReDoc 2 dark theme, expressed as a ReDoc 3
    /// ramp, so a site that moved from one of the adapters to this host can keep looking like
    /// itself.
    /// </summary>
    Midnight,

    /// <summary>
    /// The Nord palette: Polar Night surfaces, Snow Storm text, a Frost accent.
    /// </summary>
    Nord,

    /// <summary>
    /// GitHub's dark theme: its canvas and border greys with its blue accent, for docs that sit
    /// next to a repository.
    /// </summary>
    GitHubDark,
}
