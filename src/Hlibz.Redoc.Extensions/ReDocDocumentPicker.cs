using System.Text.Json;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Builds the CSS and script for the document picker rendered by
/// <c>UseReDocDocuments</c>.
/// </summary>
/// <remarks>
/// Colors come from the <c>--hlibz-control-*</c> custom properties declared in
/// <see cref="ReDocToolbar.Css"/>. The dark theme overrides them under its own root selector, so the
/// picker follows it (and the theme selector) without knowing whether either is enabled.
/// </remarks>
internal static class ReDocDocumentPicker
{
    public static string Build(
        IReadOnlyList<ReDocDocumentPage> pages,
        IReadOnlyList<ReDocLink> links,
        ReDocDocumentPage current)
    {
        const string template = """
            <style>
            {{TOOLBAR_CSS}}
              /* The <select> itself is the pill, so its whole surface opens the list; the icons sit
                 on top of its padding and let clicks through. */
              .hlibz-doc-picker {
                position: relative; order: -1; display: inline-flex; align-items: center;
                min-width: 0; color: var(--hlibz-control-fg);
              }
              .hlibz-doc-picker:hover { color: var(--hlibz-control-fg-hover); }
              .hlibz-doc-picker select {
                appearance: none; -webkit-appearance: none;
                height: 36px; max-width: 280px; margin: 0; padding: 0 30px 0 34px;
                box-sizing: border-box; border-radius: 999px;
                background: var(--hlibz-control-bg);
                border: 1px solid var(--hlibz-control-border);
                box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
                color: inherit; cursor: pointer;
                font: 500 13px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                text-overflow: ellipsis;
              }
              .hlibz-doc-picker select:focus { outline: none; }
              .hlibz-doc-picker select:focus-visible {
                outline: 2px solid var(--hlibz-control-accent); outline-offset: 1px;
              }
              .hlibz-doc-picker svg {
                position: absolute; left: 12px; width: 16px; height: 16px; pointer-events: none;
              }
              .hlibz-doc-picker .hlibz-doc-picker-chevron { left: auto; right: 10px; width: 14px; height: 14px; }
              @media (max-width: 50rem) {
                .hlibz-doc-picker { flex: 0 1 auto; }
                .hlibz-doc-picker select { max-width: 100%; }
              }
            </style>
            <script>
            (function () {
              var docs = {{DOCS}};
              var links = {{LINKS}};

            {{TOOLBAR_SCRIPT}}

              function addOption(select, entry) {
                var option = document.createElement('option');
                option.value = entry.href;
                option.textContent = entry.label;
                select.appendChild(option);
                return option;
              }

              function render() {
                var picker = document.createElement('label');
                picker.className = 'hlibz-doc-picker';
                picker.innerHTML =
                  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2 2 7l10 5 10-5-10-5z"/><path d="m2 17 10 5 10-5"/><path d="m2 12 10 5 10-5"/></svg>' +
                  '<svg class="hlibz-doc-picker-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';

                var select = document.createElement('select');
                select.setAttribute('aria-label', 'API document');
                var currentOption;
                docs.forEach(function (doc) {
                  var option = addOption(select, doc);
                  if (doc.current) { currentOption = option; }
                });
                if (links.length) {
                  // Browsers without <hr> support in <select> just skip it.
                  select.appendChild(document.createElement('hr'));
                  links.forEach(function (link) { addOption(select, link); });
                }
                currentOption.selected = true;
                // The pill truncates long names; the tooltip shows the whole one.
                picker.title = currentOption.textContent;
                select.addEventListener('change', function () { window.location.href = select.value; });
                picker.insertBefore(select, picker.lastChild);

                // Coming back via the back button restores the page from bfcache with the select
                // still showing the entry the viewer navigated to; point it back at this one.
                window.addEventListener('pageshow', function () { currentOption.selected = true; });

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

        // Every page is served at {base}/{route}/index.html, so links are relative to that folder:
        // they need no knowledge of the PathBase and keep working behind a path-rewriting proxy.
        string toBase = string.Concat(Enumerable.Repeat("../", SegmentCount(current.Route)));

        ReDocPickerEntry[] docs =
        [
            .. pages.Select(page => new ReDocPickerEntry(
                page.Document.DisplayName ?? page.Document.Name,
                Relative(
                    toBase,
                    page.Route.Length == 0 ? string.Empty : $"{EscapePath(page.Route)}/"),
                page == current)),
        ];
        ReDocPickerEntry[] linkEntries =
        [
            .. links.Select(link => new ReDocPickerEntry(
                link.Label,
                link.Url.StartsWith("~/", StringComparison.Ordinal)
                    ? Relative(toBase, link.Url[2..])
                    : link.Url)),
        ];

        // The default encoder escapes <, >, & and quotes, so the JSON is safe inside <script>.
        return template
            .Replace("{{TOOLBAR_CSS}}", ReDocToolbar.Css)
            .Replace("{{TOOLBAR_SCRIPT}}", ReDocToolbar.ScriptFunction)
            .Replace("{{DOCS}}", Serialize(docs))
            .Replace("{{LINKS}}", Serialize(linkEntries));
    }

    private static string Serialize(ReDocPickerEntry[] entries) =>
        JsonSerializer.Serialize(entries, ReDocPickerJson.Default.ReDocPickerEntryArray);

    private static int SegmentCount(string route) =>
        route.Length == 0 ? 0 : route.Split('/').Length;

    private static string Relative(string toBase, string path)
    {
        string href = toBase + path;
        return href.Length == 0 ? "./" : href;
    }

    private static string EscapePath(string route) =>
        string.Join('/', route.Split('/').Select(Uri.EscapeDataString));
}
