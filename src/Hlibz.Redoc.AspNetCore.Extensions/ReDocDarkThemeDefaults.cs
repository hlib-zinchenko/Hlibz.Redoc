using System.Text;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Builds the ReDoc <c>HeadContent</c> string and theme config dictionary consumed by
/// <see cref="RedocOptionsExtensions.UseDarkTheme"/>.
/// </summary>
internal static class ReDocDarkThemeDefaults
{
    private const string DefaultPrimaryLight = "#7dd3fc";
    private const string DefaultPrimaryDark = "#0284c7";

    public static string BuildHeadContent(DarkThemeOptions theme)
    {
        // With the selector enabled, every dark override is scoped under an attribute the
        // selector script toggles on <html>; otherwise they apply unconditionally, as before.
        string root = theme.ThemeSelectorEnabled ? ReDocThemeSelector.DarkRootSelector : "html";

        const string darkCss = """
              {{ROOT}} { color-scheme: dark; }
              {{ROOT}}, {{ROOT}} body { background-color: #09090b; }
              {{ROOT}}::-webkit-scrollbar, {{ROOT}} ::-webkit-scrollbar { width: 12px; height: 12px; }
              {{ROOT}}::-webkit-scrollbar-track, {{ROOT}} ::-webkit-scrollbar-track { background: #18181b; }
              {{ROOT}}::-webkit-scrollbar-thumb, {{ROOT}} ::-webkit-scrollbar-thumb { background-color: #3f3f46; border-radius: 6px; border: 3px solid #18181b; }
              {{ROOT}}, {{ROOT}} * { scrollbar-color: #3f3f46 #18181b; scrollbar-width: thin; }
              {{ROOT}} ::selection { background: #0ea5e9; color: #09090b; }

              /* ReDoc hardcodes these section captions to a light-theme color outside the theme
                 object (Query Parameters / Request Body schema / Response Schema), so they need a
                 direct override rather than a theme key. */
              {{ROOT}} #redoc-container h5 { color: #d4d4d8 !important; opacity: 0.85 !important; }
              {{ROOT}} #redoc-container h5 span { color: #a1a1aa !important; }

              /* ReDoc alternates nested-schema backgrounds between `schema.nestedBackground`
                 (themeable) and a hardcoded #ffffff (not themeable) every other nesting level, so
                 deeply nested schemas flash white in a dark theme. The white comes from the div
                 wrapping each nested schema table (not the table itself), and that div isn't
                 addressable via a theme key, so match it structurally instead. Requiring a table
                 ancestor keeps this scoped to genuinely nested schemas — without it, this also
                 catches the root-level wrapper around a top-level Request Body/Parameters table,
                 tinting whole operations that happen to have one. */
              {{ROOT}} #redoc-container table div:has(> table) { background: #18181b !important; }

              /* ReDoc's selected-tab style (used by the request/response sample "Payload"/language
                 tabs) hardcodes background: rightPanel.textColor with color: colors.text.primary.
                 Both of those are light in this theme, so the selected tab's own label is
                 invisible. Response status-code tabs (200/404/500...) already override their own
                 text color via tab-success/tab-error/etc. and stay readable, so they're excluded
                 here. */
              {{ROOT}} #redoc-container
                .react-tabs__tab--selected:not(.tab-success):not(.tab-error):not(.tab-redirect):not(.tab-info) {
                color: #09090b !important;
              }

            """;

        string additionalCssBlock = string.IsNullOrWhiteSpace(theme.AdditionalCss)
            ? string.Empty
            : $"{theme.AdditionalCss.Trim()}\n";

        StringBuilder head = new();
        if (!theme.ThemeSelectorEnabled)
        {
            head.Append("<meta name=\"color-scheme\" content=\"dark\">\n");
        }

        head.Append("<style>\n").Append(darkCss.Replace("{{ROOT}}", root));

        // Dark values for the tokens this package's controls (theme selector, document picker) use.
        // ReDocToolbar.Css declares their light values with lower specificity.
        head.Append($$"""
              {{root}} {
                --hlibz-control-bg: rgba(24, 24, 27, 0.92); --hlibz-control-border: #3f3f46;
                --hlibz-control-fg: #a1a1aa; --hlibz-control-fg-hover: #fafafa;
                --hlibz-control-accent: {{theme.PrimaryColor}}; --hlibz-control-accent-fg: #09090b;
              }

            """);
        if (theme.ThemeSelectorEnabled)
        {
            head.Append(ReDocToolbar.Css).Append(ReDocThemeSelector.Css);
        }

        head.Append(additionalCssBlock).Append("</style>");
        if (theme.ThemeSelectorEnabled)
        {
            head.Append('\n').Append(ReDocThemeSelector.BuildScript(theme.DefaultTheme));
        }

        return head.ToString();
    }

    public static Dictionary<string, object> BuildConfigValue(DarkThemeOptions theme)
    {
        string main = theme.PrimaryColor;
        string light = theme.PrimaryColorLight ?? DefaultLight(theme.PrimaryColor);
        string dark = theme.PrimaryColorDark ?? DefaultDark(theme.PrimaryColor);

        return new Dictionary<string, object>
        {
            ["colors"] = new Dictionary<string, object>
            {
                ["primary"] = new Dictionary<string, object>
                {
                    ["main"] = main,
                    ["light"] = light,
                    ["dark"] = dark,
                    ["contrastText"] = "#09090b",
                },
                ["success"] = new Dictionary<string, object>
                {
                    ["main"] = "#22c55e",
                    ["light"] = "#4ade80",
                    ["dark"] = "#16a34a",
                    ["contrastText"] = "#052e16",
                },
                ["warning"] = new Dictionary<string, object>
                {
                    ["main"] = "#f59e0b",
                    ["light"] = "#fbbf24",
                    ["dark"] = "#b45309",
                    ["contrastText"] = "#451a03",
                },
                ["error"] = new Dictionary<string, object>
                {
                    ["main"] = "#f43f5e",
                    ["light"] = "#fb7185",
                    ["dark"] = "#be123c",
                    ["contrastText"] = "#4c0519",
                },
                ["text"] = new Dictionary<string, object> { ["primary"] = "#fafafa", ["secondary"] = "#d4d4d8" },
                ["border"] = new Dictionary<string, object> { ["dark"] = "#3f3f46", ["light"] = "#27272a" },
                ["responses"] = new Dictionary<string, object>
                {
                    ["success"] = new Dictionary<string, object>
                    {
                        ["color"] = "#22c55e", ["backgroundColor"] = "#0f2e1c", ["tabTextColor"] = "#22c55e",
                    },
                    ["error"] = new Dictionary<string, object>
                    {
                        ["color"] = "#f43f5e", ["backgroundColor"] = "#3a0d18", ["tabTextColor"] = "#f43f5e",
                    },
                    ["redirect"] = new Dictionary<string, object>
                    {
                        ["color"] = "#f59e0b", ["backgroundColor"] = "#3a2708", ["tabTextColor"] = "#f59e0b",
                    },
                    ["info"] = new Dictionary<string, object>
                    {
                        ["color"] = main, ["backgroundColor"] = "#0c2d3d", ["tabTextColor"] = main,
                    },
                },
                ["http"] = new Dictionary<string, object>
                {
                    ["get"] = main,
                    ["post"] = "#22c55e",
                    ["put"] = "#f59e0b",
                    ["options"] = "#a78bfa",
                    ["patch"] = "#fb923c",
                    ["delete"] = "#f43f5e",
                    ["basic"] = "#a1a1aa",
                    ["link"] = "#2dd4bf",
                    ["head"] = "#c084fc",
                },
                ["gray"] = new Dictionary<string, object> { ["50"] = "#3f3f46", ["100"] = "#27272a" },
                ["tonalOffset"] = 0.2,
            },
            ["sidebar"] = new Dictionary<string, object>
            {
                ["backgroundColor"] = "#18181b",
                ["textColor"] = "#d4d4d8",
                ["activeTextColor"] = main,
                ["groupItems"] = new Dictionary<string, object>
                {
                    ["activeBackgroundColor"] = "#27272a", ["activeTextColor"] = main, ["textTransform"] = "none",
                },
                ["level1Items"] = new Dictionary<string, object>
                {
                    ["activeBackgroundColor"] = "#27272a", ["activeTextColor"] = main, ["textTransform"] = "none",
                },
                ["arrow"] = new Dictionary<string, object> { ["color"] = "#71717a" },
            },
            ["rightPanel"] = new Dictionary<string, object>
            {
                ["backgroundColor"] = "#09090b",
                ["textColor"] = "#fafafa",
                ["servers"] = new Dictionary<string, object>
                {
                    ["overlay"] = new Dictionary<string, object>
                    {
                        ["backgroundColor"] = "#18181b", ["textColor"] = "#fafafa",
                    },
                    ["url"] = new Dictionary<string, object> { ["backgroundColor"] = "#18181b" },
                },
            },
            ["codeBlock"] = new Dictionary<string, object> { ["backgroundColor"] = "#18181b" },
            ["typography"] = new Dictionary<string, object>
            {
                ["links"] = new Dictionary<string, object>
                {
                    ["color"] = main, ["visited"] = light, ["hover"] = light,
                },
                ["code"] = new Dictionary<string, object> { ["color"] = "#f59e0b", ["backgroundColor"] = "#27272a" },
            },
            ["schema"] = new Dictionary<string, object>
            {
                ["linesColor"] = "#3f3f46",
                ["typeNameColor"] = "#a1a1aa",
                ["typeTitleColor"] = main,
                ["requireLabelColor"] = "#f43f5e",
                ["nestedBackground"] = "#18181b",
            },
        };
    }

    private static string DefaultLight(string primaryColor) =>
        primaryColor == DarkThemeOptions.DefaultPrimaryColor ? DefaultPrimaryLight : ColorUtils.Lighten(primaryColor, 0.18);

    private static string DefaultDark(string primaryColor) =>
        primaryColor == DarkThemeOptions.DefaultPrimaryColor ? DefaultPrimaryDark : ColorUtils.Darken(primaryColor, 0.20);
}
