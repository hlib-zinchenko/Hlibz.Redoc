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
/// <para>
/// The CSS and the script are also exposed on their own, because a host that owns its page serves
/// them as files rather than inlining them — which is the difference between a page a strict
/// Content-Security-Policy accepts and one it doesn't.
/// </para>
/// </remarks>
internal static class ReDocDocumentPicker
{
    /// <summary>The picker's own CSS, without <see cref="ReDocToolbar.Css"/>.</summary>
    public const string Css = """
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
        """;

    /// <summary>
    /// What the searchable picker needs on top of <see cref="Css"/>, whose pill, icons and tokens
    /// it reuses. Only <c>Hlibz.Redoc.AspNetCore</c> emits it.
    /// </summary>
    /// <remarks>
    /// The popover is <c>position: fixed</c> and placed from the pill's own rect in script, not
    /// <c>absolute</c> under it. That is not a preference: ReDoc's sidebar is
    /// <c>overflow-y: auto</c>, so an absolutely positioned list inside it is clipped by the scroll
    /// container — and the sidebar is exactly where a site with enough documents to want search
    /// puts its picker.
    /// </remarks>
    public const string SearchCss = """
          .hlibz-doc-picker-button {
            appearance: none; -webkit-appearance: none;
            height: 36px; max-width: 280px; margin: 0; padding: 0 30px 0 34px;
            box-sizing: border-box; border-radius: 999px;
            background: var(--hlibz-control-bg);
            border: 1px solid var(--hlibz-control-border);
            box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
            color: inherit; cursor: pointer; text-align: left;
            font: 500 13px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
          }
          .hlibz-doc-picker-button:focus { outline: none; }
          .hlibz-doc-picker-button:focus-visible {
            outline: 2px solid var(--hlibz-control-accent); outline-offset: 1px;
          }
          .hlibz-doc-picker-pop {
            position: fixed; z-index: 1001; box-sizing: border-box;
            display: flex; flex-direction: column; min-width: 220px; padding: 6px;
            background: var(--hlibz-control-bg);
            border: 1px solid var(--hlibz-control-border);
            border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.25);
          }
          /* Every `display` above is an author rule on a class, and the `display: none` that
             backs the hidden attribute is the browser's own, so the attribute loses to it and
             hiding one of these in script needs this rule to mean anything. */
          .hlibz-doc-picker-pop[hidden],
          .hlibz-doc-picker-list[hidden],
          .hlibz-doc-picker-empty[hidden] { display: none; }
          .hlibz-doc-picker-input {
            box-sizing: border-box; width: 100%; height: 32px; margin: 0 0 6px; padding: 0 10px;
            border-radius: 999px; background: transparent;
            border: 1px solid var(--hlibz-control-border);
            color: var(--hlibz-control-fg-hover);
            font: 400 13px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          }
          .hlibz-doc-picker-input:focus-visible {
            outline: 2px solid var(--hlibz-control-accent); outline-offset: -1px;
          }
          .hlibz-doc-picker-list {
            margin: 0; padding: 0; list-style: none; overflow-y: auto; overscroll-behavior: contain;
          }
          .hlibz-doc-picker-group {
            padding: 8px 10px 4px; color: var(--hlibz-control-fg); opacity: 0.75;
            font: 600 11px/1 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            letter-spacing: 0.04em; text-transform: uppercase;
          }
          .hlibz-doc-picker-option {
            padding: 7px 10px; border-radius: 8px; cursor: pointer;
            color: var(--hlibz-control-fg); white-space: nowrap;
            overflow: hidden; text-overflow: ellipsis;
            font: 500 13px/1.2 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          }
          .hlibz-doc-picker-option:hover { color: var(--hlibz-control-fg-hover); }
          .hlibz-doc-picker-option[aria-selected="true"] { color: var(--hlibz-control-fg-hover); }
          /* The keyboard's position in the list, which is not the same thing as the page you are
             on: both can be visible at once, so they can't share a style. */
          .hlibz-doc-picker-active, .hlibz-doc-picker-active:hover {
            background: var(--hlibz-control-accent); color: var(--hlibz-control-accent-fg);
          }
          .hlibz-doc-picker-sep {
            height: 0; margin: 6px 4px; border-top: 1px solid var(--hlibz-control-border);
          }
          .hlibz-doc-picker-empty {
            margin: 0; padding: 9px 10px; color: var(--hlibz-control-fg);
            font: 400 13px/1.2 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          }
        """;

    /// <summary>
    /// The picker as a <c>&lt;style&gt;</c> plus a <c>&lt;script&gt;</c>, to append to a page's
    /// head content. This is what the adapters use, because <c>HeadContent</c> is the only seam
    /// their hosts give them.
    /// </summary>
    public static string Build(
        IReadOnlyList<ReDocDocumentPage> pages,
        IReadOnlyList<ReDocLink> links,
        ReDocDocumentPage current)
    {
        const string template = """
            <style>
            {{TOOLBAR_CSS}}
            {{PICKER_CSS}}
            </style>
            <script>
            {{SCRIPT}}
            </script>
            """;

        return template
            .Replace("{{TOOLBAR_CSS}}", ReDocToolbar.Css)
            .Replace("{{PICKER_CSS}}", Css)
            .Replace("{{SCRIPT}}", BuildScript(pages, links, current));
    }

    /// <summary>
    /// The picker's script, without the <c>&lt;script&gt;</c> tags, so a host that owns its page
    /// can serve it as a file.
    /// </summary>
    /// <param name="pages">The pages this picker lists.</param>
    /// <param name="links">The plain links listed after them.</param>
    /// <param name="current">The page this script is served from.</param>
    /// <param name="spot">
    /// Which strip this picker goes into. The picker appends to whatever container
    /// <c>hlibzToolbar()</c> hands it whatever the spot, so only
    /// <see cref="ReDocToolbar.ScriptFunctionFor"/> changes — which is what keeps
    /// <see cref="ReDocToolbarSpot.Shared"/>, the adapters' value, emitting exactly what it always
    /// has.
    /// </param>
    /// <param name="searchable">
    /// Whether to build the searchable combobox instead of the native <c>&lt;select&gt;</c>. A
    /// whole other control, so it is a whole other template rather than a placeholder in this one:
    /// the <c>&lt;select&gt;</c> path is what the adapters inline and has to stay exactly as it
    /// is.
    /// </param>
    public static string BuildScript(
        IReadOnlyList<ReDocDocumentPage> pages,
        IReadOnlyList<ReDocLink> links,
        ReDocDocumentPage current,
        ReDocToolbarSpot spot = ReDocToolbarSpot.Shared,
        bool searchable = false)
    {
        const string template = """
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

            {{GROUP_FUNCTION}}  function render() {
                var picker = document.createElement('label');
                picker.className = 'hlibz-doc-picker';
                picker.innerHTML =
                  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2 2 7l10 5 10-5-10-5z"/><path d="m2 17 10 5 10-5"/><path d="m2 12 10 5 10-5"/></svg>' +
                  '<svg class="hlibz-doc-picker-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';

                var select = document.createElement('select');
                select.setAttribute('aria-label', 'API document');
                var currentOption;
                docs.forEach(function (doc) {
                  var option = addOption({{DOC_PARENT}}, doc);
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
            """;

        // Every page is served at {base}/{route}/index.html, so links are relative to that folder:
        // they need no knowledge of the PathBase and keep working behind a path-rewriting proxy.
        ReDocPickerEntry[] docs =
        [
            .. pages.Select(page => new ReDocPickerEntry(
                page.Document.DisplayName ?? page.Document.Name,
                ReDocRelativePath.Between(
                    current.Route,
                    page.Route.Length == 0
                        ? string.Empty
                        : $"{ReDocRelativePath.EscapeRoute(page.Route)}/"),
                page == current,
                string.IsNullOrWhiteSpace(page.Document.Group)
                    ? null
                    : page.Document.Group)),
        ];

        bool grouped = Array.Exists(docs, entry => entry.Group is not null);
        ReDocPickerEntry[] linkEntries =
        [
            .. links.Select(link => new ReDocPickerEntry(
                link.Label,
                link.Url.StartsWith("~/", StringComparison.Ordinal)
                    ? ReDocRelativePath.Between(current.Route, link.Url[2..])
                    : link.Url)),
        ];

        // The default encoder escapes <, >, & and quotes, so the JSON is safe inside <script>.
        return (searchable ? SearchTemplate : template)
            .Replace("{{TOOLBAR_SCRIPT}}", ReDocToolbar.ScriptFunctionFor(spot))
            .Replace("{{GROUP_FUNCTION}}", grouped ? GroupFunction : string.Empty)
            .Replace("{{DOC_PARENT}}", grouped ? "groupFor(select, doc)" : "select")
            .Replace("{{DOCS}}", Serialize(docs))
            .Replace("{{LINKS}}", Serialize(linkEntries));
    }

    /// <summary>
    /// The searchable picker: a button, a filter box and a listbox, in place of the
    /// <c>&lt;select&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate template rather than placeholders in the other one. The two share only their
    /// data and their mounting, and the <c>&lt;select&gt;</c> version is inlined verbatim by both
    /// adapters, so the useful property is that it is impossible to change from here.
    /// </para>
    /// <para>
    /// Why a custom control at all, when a native <c>&lt;select&gt;</c> is better on a phone and
    /// free: a native one can't be filtered. Its popup isn't scriptable, its type-ahead matches
    /// only the start of a label, and it is per-OS. <c>&lt;datalist&gt;</c> filters for free but
    /// matches the value, carries no groups, and hands back a string to map home again. So the
    /// control is ours, and with it the keyboard: Enter opens, typing filters, arrows move,
    /// Enter navigates, Escape closes and returns focus to the button.
    /// </para>
    /// <para>
    /// A document matches on <em>both</em> its own label and its group's, so typing a group name
    /// brings up everything under it. A plain link has no group and matches on its label, which is
    /// all the text it has.
    /// </para>
    /// <para>
    /// Grouping follows the <c>&lt;optgroup&gt;</c> rule exactly — the heading lands where the
    /// group's first member falls, and later members join it rather than repeating it — which is
    /// why rows go in through <c>place</c> and the keyboard's list is read back out of the DOM
    /// afterwards, in the order it ended up on screen.
    /// </para>
    /// </remarks>
    private const string SearchTemplate = """
        (function () {
          var docs = {{DOCS}};
          var links = {{LINKS}};

        {{TOOLBAR_SCRIPT}}

          function matches(entry, query) {
            return (entry.label + ' ' + (entry.group || '')).toLowerCase().indexOf(query) !== -1;
          }

          function render() {
            var current;
            docs.forEach(function (doc) { if (doc.current) { current = doc; } });

            var picker = document.createElement('div');
            picker.className = 'hlibz-doc-picker hlibz-doc-picker-search';
            picker.innerHTML =
              '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2 2 7l10 5 10-5-10-5z"/><path d="m2 17 10 5 10-5"/><path d="m2 12 10 5 10-5"/></svg>' +
              '<svg class="hlibz-doc-picker-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';
            // The pill truncates long names; the tooltip shows the whole one.
            picker.title = current.label;

            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'hlibz-doc-picker-button';
            button.setAttribute('aria-haspopup', 'listbox');
            button.setAttribute('aria-expanded', 'false');
            button.textContent = current.label;

            var pop = document.createElement('div');
            pop.className = 'hlibz-doc-picker-pop';
            pop.hidden = true;

            var input = document.createElement('input');
            input.type = 'text';
            input.className = 'hlibz-doc-picker-input';
            input.setAttribute('aria-label', 'Search documents');
            input.setAttribute('autocomplete', 'off');
            input.placeholder = 'Search documents';

            var list = document.createElement('ul');
            list.className = 'hlibz-doc-picker-list';
            list.setAttribute('role', 'listbox');
            list.setAttribute('aria-label', 'API document');

            var empty = document.createElement('p');
            empty.className = 'hlibz-doc-picker-empty';
            empty.textContent = 'No documents match';
            empty.hidden = true;

            pop.appendChild(input);
            pop.appendChild(list);
            pop.appendChild(empty);
            picker.insertBefore(button, picker.lastChild);
            picker.appendChild(pop);

            var options = [];
            var active = -1;
            var tails;

            function row(className, text) {
              var item = document.createElement('li');
              item.className = className;
              item.setAttribute('role', 'presentation');
              if (text) { item.textContent = text; }
              return item;
            }

            function place(entry) {
              var item = document.createElement('li');
              item.className = 'hlibz-doc-picker-option';
              item.setAttribute('role', 'option');
              item.textContent = entry.label;
              item.hlibzHref = entry.href;
              if (entry.current) { item.setAttribute('aria-selected', 'true'); }
              // mousedown rather than click: it beats the input's blur, so the close can't cancel
              // the navigation, and preventDefault keeps focus where it is until we leave.
              item.addEventListener('mousedown', function (event) {
                event.preventDefault();
                go(item);
              });

              if (!entry.group) {
                list.appendChild(item);
              } else {
                if (!tails[entry.group]) {
                  tails[entry.group] = row('hlibz-doc-picker-group', entry.group);
                  list.appendChild(tails[entry.group]);
                }
                list.insertBefore(item, tails[entry.group].nextSibling);
                tails[entry.group] = item;
              }
            }

            function fill() {
              var query = input.value.trim().toLowerCase();
              list.textContent = '';
              tails = {};

              docs.forEach(function (doc) {
                if (!query || matches(doc, query)) { place(doc); }
              });

              var shown = list.querySelectorAll('.hlibz-doc-picker-option').length;
              var hits = [];
              links.forEach(function (link) {
                if (!query || matches(link, query)) { hits.push(link); }
              });
              if (hits.length) {
                if (shown) { list.appendChild(row('hlibz-doc-picker-sep')); }
                hits.forEach(function (link) { place(link); });
              }

              // Read back from the DOM, so the keyboard walks the list in the order it reads.
              options = Array.prototype.slice.call(
                list.querySelectorAll('.hlibz-doc-picker-option'));
              options.forEach(function (item, index) { item.id = 'hlibz-doc-option-' + index; });
              empty.hidden = options.length > 0;
              list.hidden = options.length === 0;
              setActive(options.length ? 0 : -1);
            }

            function setActive(index) {
              if (options[active]) {
                options[active].classList.remove('hlibz-doc-picker-active');
              }
              active = index;
              if (!options[index]) {
                input.removeAttribute('aria-activedescendant');
                return;
              }
              var item = options[index];
              item.classList.add('hlibz-doc-picker-active');
              input.setAttribute('aria-activedescendant', item.id);
              if (item.offsetTop < list.scrollTop) {
                list.scrollTop = item.offsetTop;
              } else if (item.offsetTop + item.offsetHeight > list.scrollTop + list.clientHeight) {
                list.scrollTop = item.offsetTop + item.offsetHeight - list.clientHeight;
              }
            }

            function go(item) {
              if (item) { window.location.href = item.hlibzHref; }
            }

            // Fixed, and placed from the pill's own rect: ReDoc's sidebar scrolls, so an absolutely
            // positioned list inside it would be clipped by the scroll container.
            function position() {
              var box = picker.getBoundingClientRect();
              var room = document.documentElement.clientWidth;
              var width = Math.max(box.width, 220);
              pop.style.width = width + 'px';
              pop.style.left = Math.max(8, Math.min(box.left, room - width - 8)) + 'px';

              var below = window.innerHeight - box.bottom - 8;
              var above = box.top - 8;
              // Flip up when the pill is near the bottom, which is where a bottom-right strip is.
              if (below < 220 && above > below) {
                pop.style.top = '';
                pop.style.bottom = (window.innerHeight - box.top + 6) + 'px';
                list.style.maxHeight = Math.max(120, above - 52) + 'px';
              } else {
                pop.style.bottom = '';
                pop.style.top = (box.bottom + 6) + 'px';
                list.style.maxHeight = Math.max(120, below - 52) + 'px';
              }
            }

            function outside(event) {
              if (!picker.contains(event.target)) { close(); }
            }

            function open() {
              if (!pop.hidden) { return; }
              pop.hidden = false;
              button.setAttribute('aria-expanded', 'true');
              input.value = '';
              fill();
              position();
              input.focus();
              document.addEventListener('mousedown', outside, true);
              window.addEventListener('resize', position);
              window.addEventListener('scroll', position, true);
            }

            function close() {
              if (pop.hidden) { return; }
              setActive(-1);
              pop.hidden = true;
              button.setAttribute('aria-expanded', 'false');
              document.removeEventListener('mousedown', outside, true);
              window.removeEventListener('resize', position);
              window.removeEventListener('scroll', position, true);
            }

            button.addEventListener('click', function () {
              if (pop.hidden) { open(); } else { close(); button.focus(); }
            });

            input.addEventListener('input', fill);

            input.addEventListener('keydown', function (event) {
              if (event.key === 'ArrowDown') {
                event.preventDefault();
                setActive(Math.min(active + 1, options.length - 1));
              } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                setActive(Math.max(active - 1, 0));
              } else if (event.key === 'Home') {
                event.preventDefault();
                setActive(options.length ? 0 : -1);
              } else if (event.key === 'End') {
                event.preventDefault();
                setActive(options.length - 1);
              } else if (event.key === 'Enter') {
                event.preventDefault();
                go(options[active]);
              } else if (event.key === 'Escape') {
                event.preventDefault();
                close();
                button.focus();
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

        """;

    /// <summary>
    /// Finds or creates the <c>&lt;optgroup&gt;</c> an entry belongs in, emitted only when some
    /// document is actually grouped.
    /// </summary>
    /// <remarks>
    /// Conditional so that a site using no groups serves the script it served before groups
    /// existed, byte for byte — which is the standard a change in core has to meet, since the two
    /// adapters emit this same script inline. Both placeholders it pairs with resolve to the old
    /// text when there is nothing to group, including the blank line this one would occupy.
    /// </remarks>
    private const string GroupFunction = """
              function groupFor(select, doc) {
                if (!doc.group) { return select; }
                var groups = select.hlibzGroups || (select.hlibzGroups = {});
                if (!groups[doc.group]) {
                  var group = document.createElement('optgroup');
                  group.label = doc.group;
                  groups[doc.group] = group;
                  // Where its first member falls, so configured order is what you see.
                  select.appendChild(group);
                }
                return groups[doc.group];
              }


            """;

    private static string Serialize(ReDocPickerEntry[] entries) =>
        JsonSerializer.Serialize(entries, ReDocPickerJson.Default.ReDocPickerEntryArray);
}
