namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Where one of this package's controls sits on the page. The document picker
/// (<see cref="ReDocDocumentsOptions.PickerPlacement"/>) and the toolbar links
/// (<see cref="ReDocDocumentsOptions.ToolbarItemsPlacement"/>) are placed independently.
/// </summary>
/// <remarks>
/// Two controls given the same placement share one strip and line up in it; given different ones,
/// each gets a strip of its own. In a horizontal strip the links come first and the picker last —
/// the links are fixed-width and the picker grows with a document's name, so a row that ends with
/// the widest element doesn't shuffle as you switch documents. In the sidebar, which is a column,
/// the picker leads.
/// </remarks>
public enum ReDocToolbarPlacement
{
    /// <summary>Floating at the top right, over ReDoc's content. The default.</summary>
    TopRight,

    /// <summary>
    /// Floating at the bottom right, out of the way of the page title and of ReDoc's own buttons
    /// at the top of its content.
    /// </summary>
    /// <remarks>
    /// On a narrow screen this strip joins the others in the page flow above the content rather
    /// than staying in the corner, where a phone puts ReDoc's own floating menu button.
    /// </remarks>
    BottomRight,

    /// <summary>
    /// At the top of ReDoc's sidebar, above its search box — where navigation already lives, and
    /// the better place for the picker once there are more documents than the corner can show.
    /// </summary>
    /// <remarks>
    /// On a narrow screen ReDoc hides its sidebar behind a floating button, so this strip comes
    /// back out over the page rather than disappearing with it.
    /// </remarks>
    Sidebar,
}
