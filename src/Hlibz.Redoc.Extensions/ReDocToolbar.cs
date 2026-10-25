namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Where one of this package's control strips sits. Core's own spelling of the host's public
/// <c>ReDocToolbarPlacement</c>, which core can't see: the enum a consumer writes belongs to the
/// host, because only the host can place anything.
/// </summary>
internal enum ReDocToolbarSpot
{
    /// <summary>
    /// The single unplaced container, carrying no placement class. **The adapters' value**, and the
    /// only one that keeps their inlined script byte-identical to what it has always emitted. They
    /// get the top right either way — there is nowhere else to put it on a page you don't own.
    /// </summary>
    Shared,

    /// <summary>Floating at the top right, in its own container.</summary>
    TopRight,

    /// <summary>Floating at the bottom right, in its own container.</summary>
    BottomRight,

    /// <summary>At the top of ReDoc's sidebar, in its own container.</summary>
    Sidebar,
}

/// <summary>
/// The container a control strip is built in — shared by the theme selector, the document picker
/// and a host's toolbar links, so whichever of them are enabled line up together whatever order
/// they render in.
/// </summary>
/// <remarks>
/// One container per <see cref="ReDocToolbarSpot"/>, found by that spot's own class: two controls
/// given the same spot share a container and therefore a row, and two given different spots each
/// get their own. The adapters only ever have <see cref="ReDocToolbarSpot.Shared"/>.
/// </remarks>
/// <remarks>
/// Both controls take their colors from the <c>--hlibz-control-*</c> tokens declared here with
/// light values. They're declared under <c>:where(html)</c> (zero specificity), so the dark
/// theme's <c>html</c> or <c>html[data-redoc-theme="dark"]</c> block overrides them.
/// </remarks>
internal static class ReDocToolbar
{
    public const string Css = """
          :where(html) {
            --hlibz-control-bg: rgba(255, 255, 255, 0.92); --hlibz-control-border: #d4d4d8;
            --hlibz-control-fg: #52525b; --hlibz-control-fg-hover: #18181b;
            --hlibz-control-accent: #32329f; --hlibz-control-accent-fg: #ffffff;
          }
          .hlibz-toolbar {
            position: fixed; top: 12px; right: 12px; z-index: 1000;
            display: flex; align-items: center; gap: 8px;
          }
          /* Below ReDoc's own "small" breakpoint the page title spans the full width, so a floating
             toolbar would cover it. Put the toolbar in the flow above the page instead. */
          @media (max-width: 50rem) {
            .hlibz-toolbar {
              position: static; justify-content: flex-end; padding: 12px 12px 0;
            }
          }

        """;

    /// <summary>
    /// Styling for the plain links a host can add to the toolbar, without
    /// <see cref="Css"/>. Shaped like the picker's pill so a row of controls reads as one set.
    /// </summary>
    /// <remarks>
    /// Only <c>Hlibz.Redoc.AspNetCore</c> emits these: it serves its scripts as files, so a link
    /// costs it nothing, while on the adapters the toolbar reaches the page through the picker's
    /// inlined script and would have nowhere to go when a single-document site has no picker.
    /// The CSS lives here anyway, with the container it belongs to and the tokens it reads.
    /// </remarks>
    public const string ItemCss = """
          .hlibz-toolbar-item {
            display: inline-flex; align-items: center; height: 36px; padding: 0 14px;
            box-sizing: border-box; border-radius: 999px;
            background: var(--hlibz-control-bg);
            border: 1px solid var(--hlibz-control-border);
            box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
            color: var(--hlibz-control-fg); text-decoration: none; white-space: nowrap;
            font: 500 13px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          }
          .hlibz-toolbar-item:hover { color: var(--hlibz-control-fg-hover); }
          .hlibz-toolbar-item:focus-visible {
            outline: 2px solid var(--hlibz-control-accent); outline-offset: 1px;
          }
        """;

    /// <summary>
    /// What a strip at the bottom right needs on top of <see cref="Css"/>, which positions the top
    /// right. Only <c>Hlibz.Redoc.AspNetCore</c> emits it.
    /// </summary>
    /// <remarks>
    /// <c>top: auto</c> is the whole of it: the base rule sets <c>top</c>, and a box can't be
    /// pinned to both edges. Below the small breakpoint neither edge applies — the base rule drops
    /// every strip into the page flow there, which is also what keeps this one off ReDoc's own
    /// floating menu button, which a phone puts in exactly this corner.
    /// </remarks>
    public const string BottomRightCss = """
          .hlibz-toolbar-bottom-right { top: auto; bottom: 12px; }
        """;

    /// <summary>
    /// What a strip needs when it sits at the top of ReDoc's sidebar instead of over the page,
    /// without <see cref="Css"/>. Only <c>Hlibz.Redoc.AspNetCore</c> emits it.
    /// </summary>
    /// <remarks>
    /// Inset to the same 16px as the sidebar's own content, so it reads as part of the navigation
    /// rather than as something dropped on top of it. The picker takes the full width — the 280px
    /// cap goes with the pill — and any links wrap underneath it, which is also the one place the
    /// picker comes <em>first</em>: a column reads top-down, so the thing you navigate with leads.
    /// The selector is the state class, not the identity one, so these rules stop applying by
    /// themselves on a phone, where this strip is back over the page and horizontal again.
    /// </remarks>
    public const string SidebarCss = """
          .hlibz-toolbar-in-menu {
            position: static; flex-wrap: wrap; padding: 12px 16px 4px;
          }
          .hlibz-toolbar-in-menu .hlibz-doc-picker { order: -1; flex: 1 0 100%; }
          .hlibz-toolbar-in-menu .hlibz-doc-picker select { width: 100%; max-width: 100%; }
        """;

    /// <summary>
    /// Puts the picker after the links in a horizontal strip, for a host that emits both into one.
    /// </summary>
    /// <remarks>
    /// The picker carries <c>order: -1</c> of its own, which is what puts it ahead of the theme
    /// selector on the adapters; this outweighs it by one class rather than changing it, because
    /// the adapters' row must not move. Links first because they are the narrow, fixed-width
    /// things and the picker is the one that grows with a document's name — a row that ends with
    /// the widest element stays put as you switch documents.
    /// </remarks>
    public const string PickerAfterItemsCss = """
          .hlibz-toolbar .hlibz-doc-picker { order: 1; }
        """;

    /// <summary>A JS function declaration returning the toolbar element, creating it on first use.</summary>
    public const string ScriptFunction = """
          function hlibzToolbar() {
            var toolbar = document.querySelector('.hlibz-toolbar');
            if (!toolbar) {
              toolbar = document.createElement('div');
              toolbar.className = 'hlibz-toolbar';
              // First in <body>: on narrow screens, where it isn't fixed, it sits above the page.
              document.body.insertBefore(toolbar, document.body.firstChild);
            }
            return toolbar;
          }
        """;

    /// <summary>
    /// The same function for a strip of its own at the top right. Identical to
    /// <see cref="ScriptFunction"/> but for the placement class, which is what lets a page carry
    /// more than one strip and have each script find its own.
    /// </summary>
    public const string TopRightScriptFunction = """
          function hlibzToolbar() {
            var toolbar = document.querySelector('.hlibz-toolbar-top-right');
            if (!toolbar) {
              toolbar = document.createElement('div');
              toolbar.className = 'hlibz-toolbar hlibz-toolbar-top-right';
              // First in <body>: on narrow screens, where it isn't fixed, it sits above the page.
              document.body.insertBefore(toolbar, document.body.firstChild);
            }
            return toolbar;
          }
        """;

    /// <summary>The same function for a strip at the bottom right.</summary>
    /// <remarks>
    /// Inserted after every strip already sitting on the page, where the others all insert
    /// themselves first. That is invisible while each is pinned to its own corner, and it is the
    /// whole point on a phone, where the breakpoint drops them all into the page flow and DOM
    /// order becomes reading order: whatever else a page carries, the bottom-right strip reads
    /// last. The scan is of <c>&lt;body&gt;</c>'s own children, because a sidebar strip is inside
    /// ReDoc's menu and could not be inserted next to.
    /// </remarks>
    public const string BottomRightScriptFunction = """
          function hlibzToolbar() {
            var toolbar = document.querySelector('.hlibz-toolbar-bottom-right');
            if (!toolbar) {
              toolbar = document.createElement('div');
              toolbar.className = 'hlibz-toolbar hlibz-toolbar-bottom-right';
              var above = document.body.querySelectorAll(':scope > .hlibz-toolbar');
              document.body.insertBefore(
                toolbar,
                above.length ? above[above.length - 1].nextSibling : document.body.firstChild);
            }
            return toolbar;
          }
        """;

    /// <summary>
    /// The same function, for a strip that belongs at the top of ReDoc's sidebar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Placement hangs off creating the container rather than off each control, which is what keeps
    /// the picker and the links together and in order: a page can serve two scripts that both call
    /// <c>hlibzToolbar</c> — the picker's and the links' — and only the first one to run creates
    /// anything. Whoever that is also arranges the moving, and the other simply appends to the
    /// element it finds, wherever it has got to.
    /// </para>
    /// <para>
    /// <c>.menu-content</c> is the one class ReDoc doesn't hash, so it is the only durable hook
    /// into a tree React owns. Measured against 3.0.1: an element inserted as its first child
    /// survives navigating the menu, switching color mode, opening and closing the search dialog,
    /// and scrolling — React reconciles its own children and leaves a stranger alone.
    /// </para>
    /// <para>
    /// Two things make this more than one <c>insertBefore</c>. ReDoc renders the sidebar after its
    /// bundle boots, so the toolbar starts over the page and moves once the sidebar exists — which
    /// also means a page where ReDoc never renders keeps its controls. And below ReDoc's own
    /// breakpoint the sidebar is <c>display: none</c> until the floating button opens it, so on a
    /// phone the toolbar comes back out rather than hiding behind a menu.
    /// </para>
    /// </remarks>
    public const string SidebarScriptFunction = """
          function hlibzToolbar() {
            var toolbar = document.querySelector('.hlibz-toolbar-sidebar');
            if (!toolbar) {
              toolbar = document.createElement('div');
              toolbar.className = 'hlibz-toolbar hlibz-toolbar-sidebar';
              // First in <body> to begin with, and whenever it has to come back out.
              document.body.insertBefore(toolbar, document.body.firstChild);
              hlibzPlaceInMenu(toolbar);
            }
            return toolbar;
          }

          function hlibzPlaceInMenu(toolbar) {
            var small = window.matchMedia('(max-width: 50rem)');

            function place() {
              var menu = small.matches ? null : document.querySelector('.menu-content');
              var parent = menu || document.body;
              if (toolbar.parentNode === parent) { return; }
              toolbar.classList.toggle('hlibz-toolbar-in-menu', !!menu);
              parent.insertBefore(toolbar, parent.firstChild);
            }

            place();
            if (!document.querySelector('.menu-content')) {
              var watch = new MutationObserver(function () {
                if (document.querySelector('.menu-content')) {
                  watch.disconnect();
                  place();
                }
              });
              watch.observe(document.body, { childList: true, subtree: true });
            }

            small.addEventListener('change', place);
          }
        """;

    /// <summary>The toolbar function for one spot.</summary>
    public static string ScriptFunctionFor(ReDocToolbarSpot spot) => spot switch
    {
        ReDocToolbarSpot.TopRight => TopRightScriptFunction,
        ReDocToolbarSpot.BottomRight => BottomRightScriptFunction,
        ReDocToolbarSpot.Sidebar => SidebarScriptFunction,
        _ => ScriptFunction,
    };
}
