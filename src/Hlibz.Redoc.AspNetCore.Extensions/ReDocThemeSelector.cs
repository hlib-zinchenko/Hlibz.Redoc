namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Builds the CSS and script for the optional Light/System/Dark theme selector enabled via
/// <see cref="DarkThemeOptions.EnableThemeSelector"/>.
/// </summary>
/// <remarks>
/// Everything is delivered through <c>HeadContent</c>, so it works with Redoc.AspNetCore's stock
/// <c>index.html</c> and with custom <c>IndexStream</c> templates alike. The script intercepts the
/// <c>window.Redoc</c> global as <c>redoc.standalone.js</c> assigns it, wraps <c>Redoc.init</c> to
/// drop the dark <c>theme</c> config when the light theme is selected (keeping just the configured
/// font families, which belong to both themes), and on a switch tears the UI down with
/// <c>Redoc.destroy</c> and re-initializes it with the other theme.
/// </remarks>
internal static class ReDocThemeSelector
{
    public const string DarkRootSelector = """html[data-redoc-theme="dark"]""";

    /// <summary>
    /// The selector's CSS. Colors come from the <c>--hlibz-control-*</c> tokens in
    /// <see cref="ReDocToolbar.Css"/>, which the dark theme overrides.
    /// </summary>
    public const string Css = """
          .hlibz-theme-selector {
            display: flex; flex: none; gap: 2px; padding: 3px; border-radius: 999px;
            background: var(--hlibz-control-bg); border: 1px solid var(--hlibz-control-border);
            box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
          }
          .hlibz-theme-selector button {
            display: inline-flex; align-items: center; justify-content: center;
            width: 28px; height: 28px; padding: 0; border: 0; border-radius: 999px;
            background: transparent; color: var(--hlibz-control-fg); cursor: pointer;
          }
          .hlibz-theme-selector button:hover { color: var(--hlibz-control-fg-hover); }
          .hlibz-theme-selector button[aria-pressed="true"] {
            background: var(--hlibz-control-accent); color: var(--hlibz-control-accent-fg);
          }
          .hlibz-theme-selector button:focus-visible {
            outline: 2px solid var(--hlibz-control-accent); outline-offset: 1px;
          }
          .hlibz-theme-selector svg { width: 16px; height: 16px; }

        """;

    public static string BuildScript(DarkThemeOptions theme)
    {
        const string script = """
            <script>
            (function () {
              var STORAGE_KEY = 'hlibz-redoc-theme';
              var root = document.documentElement;
              var systemDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)');
              var preference = readStoredPreference() || '{{DEFAULT_THEME}}';
              var theme = resolve(preference);
              // Font overrides belong to both themes, so the light one gets them back as a theme
              // of its own; null when no font family is configured.
              var lightThemeConfig = {{LIGHT_THEME_CONFIG}};
              var redoc, originalInit, darkThemeConfig, lastInit, buttons = [];

            {{TOOLBAR_SCRIPT}}

              root.setAttribute('data-redoc-theme', theme);

              function readStoredPreference() {
                try {
                  var value = localStorage.getItem(STORAGE_KEY);
                  return value === 'dark' || value === 'light' || value === 'system' ? value : null;
                } catch (e) { return null; }
              }

              function resolve(value) {
                if (value !== 'system') { return value; }
                return systemDark && systemDark.matches ? 'dark' : 'light';
              }

              function optionsFor(options) {
                var result = {};
                for (var key in options) { result[key] = options[key]; }
                if (theme === 'dark') { result.theme = darkThemeConfig; }
                else if (lightThemeConfig) { result.theme = lightThemeConfig; }
                else { delete result.theme; }
                return result;
              }

              // The bundle's exports are getter-only (webpack), so Redoc.init can't be reassigned
              // in place; hand back a copy with a wrapped init instead.
              function wrap(api) {
                if (!api || typeof api.init !== 'function' || api.hlibzThemed) { return api; }
                var wrapped = { hlibzThemed: true };
                for (var key in api) { wrapped[key] = api[key]; }
                originalInit = api.init;
                wrapped.init = function (spec, options, element, callback) {
                  options = options || {};
                  if (darkThemeConfig === undefined) { darkThemeConfig = options.theme; }
                  lastInit = { spec: spec, options: options, element: element, callback: callback };
                  return originalInit.call(api, spec, optionsFor(options), element, callback);
                };
                return wrapped;
              }

              function setPreference(next) {
                preference = next;
                try { localStorage.setItem(STORAGE_KEY, next); } catch (e) { }
                syncButtons();
                applyTheme(resolve(next));
              }

              function applyTheme(next) {
                if (next === theme) { return; }
                theme = next;
                root.setAttribute('data-redoc-theme', next);
                if (!lastInit || typeof redoc.destroy !== 'function') { return; }

                // ReDoc builds its theme once per store, so a new theme needs a fresh instance.
                // Re-init scrolls to the top; restore the reader's position once it has rendered.
                var scrollY = window.scrollY;
                var init = lastInit;
                redoc.destroy(init.element);
                originalInit.call(redoc, init.spec, optionsFor(init.options), init.element, function () {
                  window.scrollTo(0, scrollY);
                  if (typeof init.callback === 'function') { init.callback.apply(this, arguments); }
                });
              }

              function syncButtons() {
                for (var i = 0; i < buttons.length; i++) {
                  buttons[i].setAttribute('aria-pressed', String(buttons[i].value === preference));
                }
              }

              function renderSelector() {
                var group = document.createElement('div');
                group.className = 'hlibz-theme-selector';
                group.setAttribute('role', 'group');
                group.setAttribute('aria-label', 'Color theme');
                [
                  ['light', 'Light theme', '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/>'],
                  ['system', 'System theme', '<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>'],
                  ['dark', 'Dark theme', '<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>']
                ].forEach(function (item) {
                  var button = document.createElement('button');
                  button.type = 'button';
                  button.value = item[0];
                  button.title = item[1];
                  button.setAttribute('aria-label', item[1]);
                  button.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + item[2] + '</svg>';
                  button.addEventListener('click', function () { setPreference(item[0]); });
                  buttons.push(button);
                  group.appendChild(button);
                });
                syncButtons();
                hlibzToolbar().appendChild(group);
              }

              // redoc.standalone.js assigns window.Redoc from a later <script>; wrap it as it lands
              // so the page's own inline Redoc.init(...) call already gets the selected theme.
              if (window.Redoc) {
                redoc = wrap(window.Redoc);
              } else {
                Object.defineProperty(window, 'Redoc', {
                  configurable: true,
                  enumerable: true,
                  get: function () { return redoc; },
                  set: function (value) { redoc = wrap(value); }
                });
              }

              // Follow the OS live while the viewer is on "System".
              if (systemDark) {
                var onSystemChange = function () {
                  if (preference === 'system') { applyTheme(resolve('system')); }
                };
                if (systemDark.addEventListener) {
                  systemDark.addEventListener('change', onSystemChange);
                } else {
                  systemDark.addListener(onSystemChange);
                }
              }

              if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', renderSelector);
              } else {
                renderSelector();
              }
            })();
            </script>
            """;

        string defaultThemeValue = theme.DefaultTheme switch
        {
            ReDocTheme.Light => "light",
            ReDocTheme.System => "system",
            _ => "dark",
        };
        return script
            .Replace("{{TOOLBAR_SCRIPT}}", ReDocToolbar.ScriptFunction)
            .Replace("{{DEFAULT_THEME}}", defaultThemeValue)
            .Replace(
                "{{LIGHT_THEME_CONFIG}}",
                ReDocFonts.BuildThemeConfigJson(ReDocFonts.Resolve(theme)));
    }
}
