namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Which of ReDoc's two color modes a page starts in, before the viewer picks one for themselves.
/// </summary>
/// <remarks>
/// ReDoc 3 renders both light and dark itself and keeps the viewer's choice in
/// <c>localStorage</c>, so this only decides what a first-time viewer sees. Whatever they choose
/// afterwards wins.
/// </remarks>
public enum ReDocColorMode
{
    /// <summary>
    /// Follow the viewer's OS/browser preference (<c>prefers-color-scheme</c>). The default.
    /// </summary>
    System,

    /// <summary>Start light.</summary>
    Light,

    /// <summary>Start dark.</summary>
    Dark,
}
