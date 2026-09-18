using System.Text.Json;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Builds the CSS and script for the document picker rendered by
/// <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>.
/// </summary>
/// <remarks>
/// Colors come from <c>--hlibz-control-*</c> custom properties with light fallbacks. The dark theme
/// defines them under its own root selector, so the picker follows it (and the theme selector)
/// without knowing whether either is enabled.
/// </remarks>
internal static class ReDocDocumentPicker
{
    public static string Build(IReadOnlyList<ReDocDocument> documents, ReDocDocument current)
    {
        const string template = """
            <style>
            {{TOOLBAR_CSS}}
              /* The <select> itself is the pill, so its whole surface opens the list; the icons sit
                 on top of its padding and let clicks through. */
              .hlibz-doc-picker {
                position: relative; order: -1; display: inline-flex; align-items: center;
                color: var(--hlibz-control-fg, #3f3f46);
              }
              .hlibz-doc-picker:hover { color: var(--hlibz-control-fg-hover, #18181b); }
              .hlibz-doc-picker select {
                appearance: none; -webkit-appearance: none;
                height: 36px; max-width: 280px; margin: 0; padding: 0 30px 0 34px;
                box-sizing: border-box; border-radius: 999px;
                background: var(--hlibz-control-bg, rgba(255, 255, 255, 0.92));
                border: 1px solid var(--hlibz-control-border, #d4d4d8);
                box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
                color: inherit; cursor: pointer;
                font: 500 13px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                text-overflow: ellipsis;
              }
              .hlibz-doc-picker select:focus { outline: none; }
              .hlibz-doc-picker select:focus-visible {
                outline: 2px solid var(--hlibz-control-accent, #32329f); outline-offset: 1px;
              }
              .hlibz-doc-picker svg {
                position: absolute; left: 12px; width: 16px; height: 16px; pointer-events: none;
              }
              .hlibz-doc-picker .hlibz-doc-picker-chevron { left: auto; right: 10px; width: 14px; height: 14px; }
            </style>
            <script>
            (function () {
              var docs = {{DOCS}};
              var current = {{CURRENT}};

            {{TOOLBAR_SCRIPT}}

              function render() {
                var picker = document.createElement('label');
                picker.className = 'hlibz-doc-picker';
                picker.title = 'Switch API document';
                picker.innerHTML =
                  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2 2 7l10 5 10-5-10-5z"/><path d="m2 17 10 5 10-5"/><path d="m2 12 10 5 10-5"/></svg>' +
                  '<svg class="hlibz-doc-picker-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';

                var select = document.createElement('select');
                select.setAttribute('aria-label', 'API document');
                docs.forEach(function (doc) {
                  var option = document.createElement('option');
                  option.value = doc.href;
                  option.textContent = doc.label;
                  option.selected = doc.name === current;
                  select.appendChild(option);
                });
                select.addEventListener('change', function () { window.location.href = select.value; });
                picker.insertBefore(select, picker.lastChild);

                // Coming back via the back button restores the page from bfcache with the select
                // still showing the document the viewer navigated to; point it back at this one.
                window.addEventListener('pageshow', function () {
                  for (var i = 0; i < docs.length; i++) {
                    if (docs[i].name === current) { select.selectedIndex = i; }
                  }
                });

                hlibzToolbar().appendChild(picker);
              }

              if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', render);
              } else {
                render();
              }
            })();
            </script>
            """;

        // Relative hrefs keep the picker working under a PathBase: every page sits at
        // /{prefix}/{name}/index.html, so a sibling document is always one level up.
        var entries = documents.Select(doc => new
        {
            name = doc.Name,
            label = doc.DisplayName ?? doc.Name,
            href = $"../{Uri.EscapeDataString(doc.Name)}/",
        });

        // The default encoder escapes <, >, & and quotes, so the JSON is safe inside <script>.
        return template
            .Replace("{{TOOLBAR_CSS}}", ReDocToolbar.Css)
            .Replace("{{TOOLBAR_SCRIPT}}", ReDocToolbar.ScriptFunction)
            .Replace("{{DOCS}}", JsonSerializer.Serialize(entries))
            .Replace("{{CURRENT}}", JsonSerializer.Serialize(current.Name));
    }
}
