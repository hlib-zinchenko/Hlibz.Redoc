namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// The fixed top-right container shared by the theme selector and the document picker, so the two
/// controls line up regardless of which one is enabled or renders first.
/// </summary>
internal static class ReDocToolbar
{
    public const string Css = """
          .hlibz-toolbar {
            position: fixed; top: 12px; right: 12px; z-index: 1000;
            display: flex; align-items: center; gap: 8px;
          }

        """;

    /// <summary>A JS function declaration returning the toolbar element, creating it on first use.</summary>
    public const string ScriptFunction = """
          function hlibzToolbar() {
            var toolbar = document.querySelector('.hlibz-toolbar');
            if (!toolbar) {
              toolbar = document.createElement('div');
              toolbar.className = 'hlibz-toolbar';
              document.body.appendChild(toolbar);
            }
            return toolbar;
          }
        """;
}
