using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// What <c>MapReDocDocuments</c> actually serves, over a real server.
/// </summary>
public class ReDocPagesTests
{
    [Fact]
    public async Task Page_HasNoInlineScriptOrStyle()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        // The whole reason the config, the picker and the color-mode bootstrap are served as
        // files: a page with anything inline needs 'unsafe-inline' or a nonce in script-src, and
        // then there is little point owning the template at all.
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Regex.Matches(html, "<script(?![^>]*\\bsrc=)", RegexOptions.IgnoreCase));
        Assert.DoesNotContain("onload=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Page_ReferencesItsFilesRelatively()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("href=\"redoc-page.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"redoc-colormode.js\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"module\" src=\"redoc-init.js\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_EncodesTheDocumentTitle()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes", "Taxes <script>alert(1)</script>")]);

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<title>Taxes &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Taxes <script>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReDocColorMode.Dark, "dark")]
    [InlineData(ReDocColorMode.Light, "light")]
    public async Task Page_StampsAKnownColorModeBeforeTheFirstByte(
        ReDocColorMode mode, string expected)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.DefaultColorMode = mode);

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains($"<html lang=\"en\" class=\"{expected}\"", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<meta name=\"color-scheme\" content=\"{expected}\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_WithSystemColorMode_LeavesTheClassToTheScript()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains(
            "<meta name=\"color-scheme\" content=\"light dark\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ColorModeScript_ReadsReDocsOwnStorageKey()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string script = await GetAsync(app, "/api-docs/taxes/redoc-colormode.js");

        // ReDoc 3 keeps the viewer's choice here and stamps the same class; this file does it
        // from a served script because ReDoc's own version of it is inline and a strict CSP
        // blocks it.
        Assert.Contains("localStorage.getItem('redoc.colorMode')", script, StringComparison.Ordinal);
        Assert.Contains("classList.add(mode)", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReDocColorMode.Dark, "dark")]
    [InlineData(ReDocColorMode.Light, "light")]
    public async Task ColorModeScript_RecordsAConfiguredDefaultWhereReDocReadsIt(
        ReDocColorMode mode, string expected)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.DefaultColorMode = mode);

        string script = await GetAsync(app, "/api-docs/taxes/redoc-colormode.js");

        // Stamping the class is not enough: ReDoc's store resolves the mode from this key first
        // and prefers-color-scheme last, then writes the class itself — so for a viewer with no
        // stored preference it would overwrite the server's choice with the OS's, and
        // DefaultColorMode would do nothing for exactly the people it is for. Measured in the
        // browser before and after: a Dark default rendered light on a light OS.
        Assert.Contains(
            "localStorage.setItem('redoc.colorMode', mode)", script, StringComparison.Ordinal);
        Assert.Contains($"mode = '{expected}';", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ColorModeScript_WithSystemColorMode_PinsNothing()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.DefaultColorMode = ReDocColorMode.System);

        string script = await GetAsync(app, "/api-docs/taxes/redoc-colormode.js");

        // The point of System is to keep following the OS. Writing the resolved mode would pin
        // the page to whatever it happened to be on the viewer's first visit.
        Assert.DoesNotContain("localStorage.setItem", script, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: dark", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitScript_CarriesTheSpecUrlAndTheConfig()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string init = await GetAsync(app, "/api-docs/taxes/redoc-init.js");

        Assert.Contains("import { init } from", init, StringComparison.Ordinal);
        Assert.Contains("\"/openapi/taxes.json\"", init, StringComparison.Ordinal);
        // Off unless the app opts in: a documentation page shouldn't call a third party.
        Assert.Contains("\"disableTelemetry\":true", init, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitScript_ImportsTheSharedBundle_FromEveryRouteDepth()
    {
        await using ReDocTestApp shallow = await ReDocTestApp.StartAsync(
            configure: docs => docs.RoutePrefix = string.Empty);
        await using ReDocTestApp deep = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes", Route: "a/b/c"), new ReDocDocument("fees")]);

        Assert.Contains(
            "\"../_redoc/redoc.standalone.js\"",
            await GetAsync(shallow, "/taxes/redoc-init.js"),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"../../../api-docs/_redoc/redoc.standalone.js\"",
            await GetAsync(deep, "/a/b/c/redoc-init.js"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PickerScript_IsAFileListingEveryDocument()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");

        Assert.DoesNotContain("<script", picker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            """var docs = [{"label":"locations","href":"../../api-docs/locations/","current":false},"""
            + """{"label":"taxes","href":"../../api-docs/taxes/","current":true}];""",
            picker,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ByDefault_BothControlsShareTheTopRightStrip()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.AddToolbarItem("Changelog", "/changelog"));

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");
        string toolbar = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // One container, found by its class, so whichever script runs first creates it and the
        // other appends to it.
        foreach (string script in new[] { picker, toolbar })
        {
            Assert.Contains(
                "document.querySelector('.hlibz-toolbar-top-right')",
                script,
                StringComparison.Ordinal);
            Assert.DoesNotContain("menu-content", script, StringComparison.Ordinal);
        }

        // A horizontal strip reads links-then-picker, and nothing else needs positioning.
        Assert.Contains(
            ".hlibz-toolbar .hlibz-doc-picker { order: 1; }", css, StringComparison.Ordinal);
        Assert.DoesNotContain("hlibz-toolbar-bottom-right", css, StringComparison.Ordinal);
        Assert.DoesNotContain("hlibz-toolbar-in-menu", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchablePicker_IsOffByDefault()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // The native control is the better one while the whole list fits on screen, and it is the
        // one both adapters inline.
        Assert.Contains("document.createElement('select')", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("hlibz-doc-picker-pop", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("hlibz-doc-picker-pop", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchablePicker_FiltersOnTheDisplayNameAndTheGroup()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [
                new ReDocDocument("taxes", "Taxes", Group: "Billing"),
                new ReDocDocument("locations", "Locations"),
            ],
            docs => docs.SearchablePicker = true);

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // Both fields in one haystack, so typing a group name brings up everything under it.
        Assert.Contains(
            "(entry.label + ' ' + (entry.group || '')).toLowerCase().indexOf(query)",
            picker,
            StringComparison.Ordinal);
        Assert.Contains("\"group\":\"Billing\"", picker, StringComparison.Ordinal);

        // A different control, so a different script and its own rules.
        Assert.Contains("hlibz-doc-picker-button", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("document.createElement('select')", picker, StringComparison.Ordinal);
        Assert.Contains(".hlibz-doc-picker-pop {", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchablePicker_IsStillAListboxForTheKeyboard()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.SearchablePicker = true);

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");

        // Replacing a native select means owning everything it gave away for free.
        Assert.Contains("'role', 'listbox'", picker, StringComparison.Ordinal);
        Assert.Contains("'aria-activedescendant'", picker, StringComparison.Ordinal);
        Assert.Contains("'aria-expanded'", picker, StringComparison.Ordinal);
        foreach (string key in new[] { "ArrowDown", "ArrowUp", "Enter", "Escape" })
        {
            Assert.Contains($"event.key === '{key}'", picker, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SearchablePicker_PositionsItsListWithScriptRatherThanCss()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs =>
            {
                docs.SearchablePicker = true;
                docs.PickerPlacement = ReDocToolbarPlacement.Sidebar;
            });

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // ReDoc's sidebar scrolls, so an absolutely positioned list inside it would be clipped by
        // the scroll container — and the sidebar is where a site with enough documents to want
        // search puts its picker.
        Assert.Contains(
            ".hlibz-doc-picker-pop {\n    position: fixed;", css, StringComparison.Ordinal);
        Assert.Contains("picker.getBoundingClientRect()", picker, StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener('scroll', position, true)",
            picker,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchablePicker_StaysShutUntilItIsOpened()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.SearchablePicker = true);

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // The script hides the popover with the hidden attribute, which is backed by a rule in the
        // browser's own stylesheet — so the `display` a class of ours sets outranks it and the
        // attribute means nothing without this. Left out, the popover is on screen from the moment
        // it is built, over its own pill: you see the filter box, clicking it never runs `open`,
        // and nothing closes it.
        Assert.Contains(".hlibz-doc-picker-pop[hidden],", css, StringComparison.Ordinal);
        Assert.Contains(
            ".hlibz-doc-picker-empty[hidden] { display: none; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlacementsAreIndependent_SoTheTwoControlsCanSplitUp()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs =>
            {
                docs.PickerPlacement = ReDocToolbarPlacement.Sidebar;
                docs.ToolbarItemsPlacement = ReDocToolbarPlacement.BottomRight;
                docs.AddToolbarItem("Changelog", "/changelog");
            });

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");
        string toolbar = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // Each script builds its own strip, so neither can find the other's.
        Assert.Contains(
            "document.querySelector('.hlibz-toolbar-sidebar')", picker, StringComparison.Ordinal);
        Assert.Contains("hlibzPlaceInMenu(toolbar);", picker, StringComparison.Ordinal);
        Assert.Contains(
            "document.querySelector('.hlibz-toolbar-bottom-right')",
            toolbar,
            StringComparison.Ordinal);
        Assert.DoesNotContain("menu-content", toolbar, StringComparison.Ordinal);

        Assert.Contains(".hlibz-toolbar-bottom-right {", css, StringComparison.Ordinal);
        Assert.Contains(".hlibz-toolbar-in-menu {", css, StringComparison.Ordinal);

        // Nothing shares a row, so there is no order to state.
        Assert.DoesNotContain("order: 1;", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedSidebarStrip_PutsThePickerFirstInstead()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs =>
            {
                docs.PickerPlacement = ReDocToolbarPlacement.Sidebar;
                docs.ToolbarItemsPlacement = ReDocToolbarPlacement.Sidebar;
                docs.AddToolbarItem("Changelog", "/changelog");
            });

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // A column reads top-down, so the thing you navigate with leads — the opposite of a row,
        // and the sidebar rule comes last so it outweighs the row rule rather than fighting it.
        Assert.Contains(
            ".hlibz-toolbar-in-menu .hlibz-doc-picker { order: -1;",
            css,
            StringComparison.Ordinal);
        Assert.True(
            css.IndexOf(".hlibz-toolbar .hlibz-doc-picker { order: 1; }", StringComparison.Ordinal)
            < css.IndexOf(".hlibz-toolbar-in-menu .hlibz-doc-picker", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BottomRightStrip_SitsUnderTheTopRightOneInTheDom()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs =>
            {
                docs.ToolbarItemsPlacement = ReDocToolbarPlacement.BottomRight;
                docs.AddToolbarItem("Changelog", "/changelog");
            });

        string toolbar = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");

        // Invisible while each is pinned to its own corner, and the whole point on a phone, where
        // the breakpoint drops them all into the page flow and DOM order is reading order.
        Assert.Contains(
            "document.body.querySelectorAll(':scope > .hlibz-toolbar')",
            toolbar,
            StringComparison.Ordinal);
        Assert.Contains(
            "above[above.length - 1].nextSibling", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SidebarPlacement_WithLinksAndNoPicker_StillReachesTheSidebar()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes")],
            docs =>
            {
                docs.ToolbarItemsPlacement = ReDocToolbarPlacement.Sidebar;
                docs.AddToolbarItem("Changelog", "/changelog");
            });

        string toolbar = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        // A single-document site has no picker, so the links' own script is the only one that can
        // place the strip.
        Assert.Contains("hlibzPlaceInMenu(toolbar);", toolbar, StringComparison.Ordinal);
        Assert.Contains(".hlibz-toolbar-in-menu", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SidebarPlacement_StillHasSomewhereToGoWhenTheSidebarIsnt()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.PickerPlacement = ReDocToolbarPlacement.Sidebar);

        string picker = await GetAsync(app, "/api-docs/taxes/redoc-picker.js");

        // Two cases, both measured in a browser against 3.0.1: ReDoc renders its sidebar after the
        // bundle boots, and below its own breakpoint the sidebar is display:none until the floating
        // button opens it. Either way the controls belong over the page rather than nowhere.
        Assert.Contains("var parent = menu || document.body;", picker, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", picker, StringComparison.Ordinal);
        Assert.Contains(
            "window.matchMedia('(max-width: 50rem)')", picker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SingleDocument_GetsNoPicker()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes")]);

        string html = await GetAsync(app, "/api-docs/taxes/");
        HttpResponseMessage picker = await app.GetAsync("/api-docs/taxes/redoc-picker.js");

        Assert.DoesNotContain("redoc-picker.js", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, picker.StatusCode);
    }

    [Theory]
    [InlineData("/base/api-docs")]
    [InlineData("/base/api-docs/")]
    public async Task RoutePrefix_RedirectsToTheFirstDocument(string path)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(pathBase: "/base");

        HttpResponseMessage response = await app.GetAsync(path);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/base/api-docs/locations/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RoutePrefix_WithTheRedirectOff_IsNotServed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.RedirectToFirstDocument = false);

        HttpResponseMessage response = await app.GetAsync("/api-docs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PageFolderWithoutItsTrailingSlash_Redirects()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await app.GetAsync("/api-docs/taxes");

        // Serving the page here would leave every relative href on it resolving one folder up.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("taxes/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task IndexHtml_ServesTheSamePageAsTheFolder()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        Assert.Equal(
            await GetAsync(app, "/api-docs/taxes/"),
            await GetAsync(app, "/api-docs/taxes/index.html"));
    }

    [Fact]
    public async Task Stylesheet_CarriesTheToolbarAndPickerRules()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        Assert.Contains(".hlibz-toolbar", css, StringComparison.Ordinal);
        Assert.Contains(".hlibz-doc-picker", css, StringComparison.Ordinal);
        // The controls borrow ReDoc 3's own variables rather than carrying a palette.
        Assert.Contains("var(--layer-color", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stylesheet_EndsWithTheCallersOwnCss()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.AdditionalCss = ":root { --color-primary-base: #ff0000; }");

        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        Assert.EndsWith(
            ":root { --color-primary-base: #ff0000; }\n", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Favicon_IsDeclaredWhenSet()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.Favicon = "/favicon.svg");

        string html = await GetAsync(app, "/api-docs/taxes/");

        Assert.Contains("<link rel=\"icon\" href=\"/favicon.svg\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlistedDocument_IsLeftOutOfAListedPagesPicker()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [
                new ReDocDocument("public"),
                new ReDocDocument("partners"),
                new ReDocDocument("internal", Unlisted: true),
            ]);

        string picker = await GetAsync(app, "/api-docs/public/redoc-picker.js");

        Assert.Contains("\"label\":\"partners\"", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("internal", picker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlistedDocument_StillSeesEverythingFromItsOwnPage()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [
                new ReDocDocument("public"),
                new ReDocDocument("internal", Unlisted: true),
                new ReDocDocument("secret", Unlisted: true),
            ]);

        string picker = await GetAsync(app, "/api-docs/internal/redoc-picker.js");

        // Whoever got here can navigate normally; hiding the links from them would only make the
        // internal documents harder to use, not harder to reach.
        Assert.Contains("\"label\":\"public\"", picker, StringComparison.Ordinal);
        Assert.Contains("\"label\":\"secret\"", picker, StringComparison.Ordinal);
        Assert.Contains("\"label\":\"internal\"", picker, StringComparison.Ordinal);
        Assert.Contains("\"current\":true", picker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlistedDocument_LeavesTheOnlyListedPageWithNoPickerAtAll()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("public"), new ReDocDocument("internal", Unlisted: true)]);

        string html = await GetAsync(app, "/api-docs/public/");
        string css = await GetAsync(app, "/api-docs/public/redoc-page.css");

        // A dropdown holding only the page you are on is worse than no dropdown, and its rules
        // shouldn't be in the stylesheet either.
        Assert.DoesNotContain("redoc-picker.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".hlibz-doc-picker", css, StringComparison.Ordinal);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await app.GetAsync("/api-docs/public/redoc-picker.js")).StatusCode);

        // The unlisted page still has one, because from there there is somewhere to go.
        string unlisted = await GetAsync(app, "/api-docs/internal/");
        Assert.Contains("redoc-picker.js", unlisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlistedDocument_IsStillServed()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("public"), new ReDocDocument("internal", Unlisted: true)]);

        // Unlisted hides the link and nothing more. Saying so in a test, because a reader who
        // assumed otherwise would ship an unprotected page.
        HttpResponseMessage response = await app.GetAsync("/api-docs/internal/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RememberLastDocument_WhenOff_SetsNoCookieAndAlwaysRedirectsToTheFirst()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage page = await app.GetAsync("/api-docs/taxes/");
        HttpResponseMessage prefix = await app.GetAsync(
            "/api-docs", ("Cookie", "hlibz.redoc.document=taxes"));

        // Off by default: a documentation page that sets no cookies is worth more to some people
        // than this feature is, so it has to be asked for.
        Assert.False(page.Headers.Contains("Set-Cookie"));
        Assert.Equal("/api-docs/locations/", prefix.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RememberLastDocument_SendsAReturningViewerBackToTheirDocument()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.RememberLastDocument = true);

        HttpResponseMessage page = await app.GetAsync("/api-docs/taxes/");
        string cookie = page.Headers.GetValues("Set-Cookie").Single();
        HttpResponseMessage prefix = await app.GetAsync(
            "/api-docs", ("Cookie", "hlibz.redoc.document=taxes"));

        Assert.Contains("hlibz.redoc.document=taxes", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api-docs", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/api-docs/taxes/", prefix.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RememberLastDocument_IgnoresANameThatIsNotADocumentHere()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.RememberLastDocument = true);

        HttpResponseMessage prefix = await app.GetAsync(
            "/api-docs", ("Cookie", "hlibz.redoc.document=../../somewhere-else"));

        // The cookie is a name to look up, never a path to follow, so a stale or edited one falls
        // back to the first page instead of going anywhere of its own choosing.
        Assert.Equal("/api-docs/locations/", prefix.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RememberLastDocument_DoesNotRememberAnUnlistedDocument()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("public"), new ReDocDocument("internal", Unlisted: true)],
            docs => docs.RememberLastDocument = true);

        HttpResponseMessage page = await app.GetAsync("/api-docs/internal/");
        HttpResponseMessage prefix = await app.GetAsync(
            "/api-docs", ("Cookie", "hlibz.redoc.document=internal"));

        // Unlisted is usually the gated one. Sending a viewer whose access has lapsed to a page
        // that answers 401 is worse than sending them to the public one.
        Assert.False(page.Headers.Contains("Set-Cookie"));
        Assert.Equal("/api-docs/public/", prefix.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ToolbarItems_AreServedAsTheirOwnScript()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs
                .AddToolbarItem("Postman", "https://example.test/postman", newTab: true)
                .AddToolbarItem("Spec", "~/openapi/taxes.json"));

        string html = await GetAsync(app, "/api-docs/taxes/");
        string script = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");

        Assert.Contains("<script src=\"redoc-toolbar.js\"></script>", html, StringComparison.Ordinal);
        Assert.Contains("\"label\":\"Postman\"", script, StringComparison.Ordinal);
        Assert.Contains("\"newTab\":true", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolbarItems_ResolveATildeUrlAgainstThePagesOwnDepth()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            [new ReDocDocument("taxes"), new ReDocDocument("deep", Route: "a/b/c")],
            docs => docs.AddToolbarItem("Spec", "~/openapi/taxes.json"));

        string shallow = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");
        string deep = await GetAsync(app, "/a/b/c/redoc-toolbar.js");

        // Same rule the picker's links follow: no PathBase baked in, and it survives a proxy that
        // rewrites paths.
        Assert.Contains("\"../../openapi/taxes.json\"", shallow, StringComparison.Ordinal);
        Assert.Contains("\"../../../openapi/taxes.json\"", deep, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolbarItems_WhenNoneAreAdded_AddNothingToThePage()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string html = await GetAsync(app, "/api-docs/taxes/");
        string css = await GetAsync(app, "/api-docs/taxes/redoc-page.css");

        Assert.DoesNotContain("redoc-toolbar.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".hlibz-toolbar-item", css, StringComparison.Ordinal);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await app.GetAsync("/api-docs/taxes/redoc-toolbar.js")).StatusCode);
    }

    [Fact]
    public async Task ToolbarItems_PutNothingInlineInThePage()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.AddToolbarItem("Portal", "/portal"));

        string html = await GetAsync(app, "/api-docs/taxes/");

        // The reason a toolbar item is a link and not an HTML escape hatch: the page has to stay
        // servable under script-src 'self' with no nonce.
        Assert.Empty(Regex.Matches(html, "<script(?![^>]*\\bsrc=)", RegexOptions.IgnoreCase));
        Assert.DoesNotContain("Portal", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolbarItem_WithMarkupInItsLabel_StaysText()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.AddToolbarItem("<b>v2</b>", "/v2"));

        string script = await GetAsync(app, "/api-docs/taxes/redoc-toolbar.js");

        // Written through textContent, and the JSON escapes the angle brackets on the way in, so
        // a label can't close the script element it sits in either.
        Assert.Contains("link.textContent = item.label;", script, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentSecurityPolicy_WhenUnset_IsNotSent()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        HttpResponseMessage response = await app.GetAsync("/api-docs/taxes/");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ContentSecurityPolicy_IsSentWithThePageOnly()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.UseDefaultContentSecurityPolicy());

        HttpResponseMessage page = await app.GetAsync("/api-docs/taxes/");
        HttpResponseMessage style = await app.GetAsync("/api-docs/taxes/redoc-page.css");

        Assert.Equal(
            ReDocDocumentsOptions.DefaultContentSecurityPolicy,
            page.Headers.GetValues("Content-Security-Policy").Single());

        // A policy on a stylesheet governs nothing; the page's own already decides what it loads.
        Assert.False(style.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ContentSecurityPolicy_DoesNotReplaceOneTheAppAlreadySet()
    {
        const string appPolicy = "default-src 'none'";
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: docs => docs.UseDefaultContentSecurityPolicy(),
            configureApp: builder => builder.Use((context, next) =>
            {
                context.Response.Headers.ContentSecurityPolicy = appPolicy;
                return next(context);
            }));

        HttpResponseMessage response = await app.GetAsync("/api-docs/taxes/");

        // The app's policy covers more than these pages, so it is the one that should hold.
        Assert.Equal(appPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public void ContentSecurityPolicy_WithALineBreak_ThrowsWhereItIsSet()
    {
        ReDocDocumentsOptions options = new();

        // A newline in a header value is header injection, not a broken page.
        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => options.ContentSecurityPolicy = "default-src 'self'\r\nX-Evil: 1");
        Assert.Equal(nameof(ReDocDocumentsOptions.ContentSecurityPolicy), thrown.ParamName);
    }

    [Fact]
    public void DefaultContentSecurityPolicy_NeedsNoNonceAndNoInlineScript()
    {
        string policy = ReDocDocumentsOptions.DefaultContentSecurityPolicy;

        // The two halves of the claim this package is built on: scripts are plain 'self' because
        // nothing is inline, and styles are not, because styled-components injects rules at
        // runtime and a nonce doesn't reach them.
        Assert.Contains("script-src 'self';", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("nonce", policy, StringComparison.Ordinal);
        Assert.Contains("style-src 'self' 'unsafe-inline'", policy, StringComparison.Ordinal);
    }

    private static Task<string> GetAsync(ReDocTestApp app, string path) =>
        app.GetStringAsync(path);
}
