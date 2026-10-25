namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The top-right container shared by the theme selector and the document picker, so the two
/// controls line up regardless of which one is enabled or renders first.
/// </summary>
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
}
