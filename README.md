![Hlibz.Redoc.AspNetCore.Extensions icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/main/docs/icon.png)

# ReDoc for ASP.NET Core

Four NuGet packages that do two different jobs:

- **Serve [ReDoc](https://github.com/Redocly/redoc) 3 from your own app.**
  `Hlibz.Redoc.AspNetCore` embeds the ReDoc 3 bundle and serves its own page, so it depends on no
  other ReDoc package. Pages go behind endpoint routing — `.RequireAuthorization()` works — the
  bundle is cached for a year, and nothing on the page is inline, so a strict
  `Content-Security-Policy` needs no nonce.
- **Add a dark theme to the ReDoc middleware you already use.** Two adapters, for
  [Swashbuckle.AspNetCore.ReDoc](https://www.nuget.org/packages/Swashbuckle.AspNetCore.ReDoc) and
  [Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore), fix the colors ReDoc 2
  hardcodes outside its theme config and add a Light/System/Dark selector.

All three ship the **document picker**: several OpenAPI documents, each on its own ReDoc page, with
a dropdown to switch between them, like Swagger UI's definition selector. ReDoc itself has nothing
like it on any version.

[![CI](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Hlibz.Redoc.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.Extensions)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hlibz.Redoc.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.Extensions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/LICENSE)

![Dark ReDoc UI screenshot](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/main/docs/screenshot.png)

<sub>The dark theme on Redoc.AspNetCore, with the picker top-right.</sub>

## Which package do I install?

Install **one**. Each adapter brings in `Hlibz.Redoc.Extensions`, the shared engine, on its own —
you never reference that directly.

| Your situation | Install | Why |
|---|---|---|
| Starting fresh, no ReDoc middleware yet | `Hlibz.Redoc.AspNetCore` | ReDoc 3, no third-party host package, pages you can put behind auth |
| Already on `Swashbuckle.AspNetCore.ReDoc`, want it dark | `Hlibz.Redoc.Extensions.Swashbuckle` | Adds the theme to the page that host already serves |
| Already on `Redoc.AspNetCore`, want it dark | `Hlibz.Redoc.Extensions.RedocAspNetCore` | Same, for the other host |
| Already on either, want the **picker** and nothing else | the matching adapter | `UseReDocDocuments` works without the theme |
| On `Hlibz.Redoc.AspNetCore.Extensions` 1.x | `Hlibz.Redoc.Extensions.RedocAspNetCore` | Renamed, same code. A package swap and a `using` change; no API change |

```bash
dotnet add package Hlibz.Redoc.AspNetCore               # our own host, ReDoc 3
dotnet add package Hlibz.Redoc.Extensions.Swashbuckle   # dark theme for Swashbuckle's ReDoc
dotnet add package Hlibz.Redoc.Extensions.RedocAspNetCore  # dark theme for Redoc.AspNetCore
```

Either way, `using Hlibz.Redoc.Extensions;` — the namespace is the same in all of them.

**The dark theme is a ReDoc 2 feature, and that is on purpose.** Both existing hosts are frozen on
ReDoc 2 (Swashbuckle 10.3.1 embeds 2.5.4, Redoc.AspNetCore embeds 2.0.0), which has no dark mode
and hardcodes some colors outside its own theme config. ReDoc 3 themes itself through ~2,900 CSS
custom properties and ships dark mode, a color-mode toggle and a system font stack built in — so
`Hlibz.Redoc.AspNetCore` has no `UseDarkTheme`, no theme selector and no fonts feature. There is
nothing left for them to do.

---

# Hlibz.Redoc.AspNetCore — ReDoc 3 from your own app

No `Redoc.AspNetCore`, no `Swashbuckle`. The package carries the ReDoc 3.0.1 bundle and serves the
page itself.

## Quick start

```csharp
builder.Services.AddOpenApi();

// ...

app.MapOpenApi();
app.MapReDocDocuments([new ReDocDocument("v1", "My API")]);
```

That serves `/api-docs/v1/`, and `/api-docs` redirects to it. The spec is read from
`/openapi/{documentName}.json`, which is where `MapOpenApi()` puts it.

Several documents get a picker in the top-right corner — see
[Multiple OpenAPI documents](#multiple-openapi-documents) below, which works the same on all three
packages.

## Color mode

ReDoc 3 has its own light/dark toggle and remembers the viewer's choice in `localStorage`. All this
package decides is what a first-time viewer sees:

```csharp
app.MapReDocDocuments(documents, docs => docs.DefaultColorMode = ReDocColorMode.Dark);
```

`System` is the default and follows `prefers-color-scheme`. The mode is stamped on `<html>` before
the first byte goes out, and the rest is done by a served script that reads ReDoc's own storage key.
ReDoc does that part with an *inline* script, which a strict CSP blocks — so without this the page
would render light for a moment first.

A configured `Light` or `Dark` is also recorded under that key, `localStorage['redoc.colorMode']`,
for a viewer who has no preference stored yet. That is not belt-and-braces: ReDoc's store resolves
the mode from that key first and `prefers-color-scheme` last and then sets the class *itself*, so
stamping the markup alone gets overwritten and the setting does nothing for precisely the
first-time viewers it is meant for. A viewer who has chosen a mode already keeps it — their value
is in that key and nothing here overwrites it — and `System` writes nothing at all, so the page
goes on following the OS rather than being pinned to whatever it was on their first visit.

## Printing

Printed pages come out light whatever mode the screen is in, and without the picker or the toolbar.
Set `PrintStyles = false` to leave printing entirely to ReDoc.

ReDoc 3 does most of this itself — its own `@media print` rules hide its sidebar and buttons and
give the panels the full width — so there is far less here than in the dark theme for ReDoc 2. What
it doesn't handle is color: browsers drop background colors when printing but keep text colors, so
a page printed from dark mode would be near-white text on white paper. ReDoc 3's dark mode turns
out to be its grey ramp reversed — every semantic token reads `var(--color-warm-grey-N)` in both
modes, and only those eleven values differ — so putting the light ramp back for the printed copy
fixes text, borders, panels and code blocks in one go. If you set both accents, the printed copy
uses `PrimaryColor`: the dark one was chosen to carry on a near-black page, not on paper.

## Toolbar links

The toolbar — a strip at the top right unless
[`ToolbarItemsPlacement`](#where-the-controls-sit) says otherwise — takes links of your own:

```csharp
docs.AddToolbarItem("Postman collection", "~/postman/widgets.json", newTab: true)
    .AddToolbarItem("Changelog", "/changelog");
```

They render as buttons in the order you add them — before the picker when they share its corner,
after it when they share the sidebar — and they take their colors from the same tokens the picker
does, so they follow the color mode and your accent without being told.
A `~/` URL resolves against the app's base path like everything else here, so it survives a
`PathBase` or a path-rewriting proxy; an absolute URL or a root-relative path is used as-is.

This is for the links a documentation site wants and this package shouldn't ship one at a time: a
Postman collection, a changelog, the dev portal, a "Try it" console rendered by something else. For
a link to **another ReDoc page of your own**, prefer `Links`, which lists it inside the picker where
a reader is already looking for documents.

A toolbar item is a link and nothing else — there is deliberately no raw HTML, CSS or JS escape
hatch. Markup or a script supplied per page would have to be inlined into it, and a page with
anything inline needs `'unsafe-inline'` or a nonce in `script-src`, which is the one property this
package exists to keep. `AdditionalCss` is served as a file and can restyle these buttons as far as
CSS reaches.

Toolbar links are `Hlibz.Redoc.AspNetCore` only. On the adapters the toolbar reaches the page
through the picker's inlined script, so a single-document site — which has no picker — would have
nowhere to put them.

## Searching the picker

Past a handful of documents a dropdown stops being a way to find anything. Turn the picker into a
searchable list:

```csharp
docs.SearchablePicker = true;
```

Typing filters on a document's display name **and** its `Group`, so a group name brings up
everything under it — `supply` finds both documents in a "Supply chain" group without either being
named for it. Group headings stay above whatever still matches, and `Links` entries, which have no
group, filter on their label.

The keyboard owns it, because replacing a native `<select>` means replacing what one gave away for
free: Enter or Space opens, typing filters, ↑/↓ and Home/End move, Enter follows the highlighted
entry, Escape closes and puts focus back on the button. The list is a `listbox` with
`aria-activedescendant`, and the entry you are on is marked `aria-selected` — distinct from where
the keyboard is, since both are visible at once.

It is **off by default**, and that is not caution: while the whole list fits on screen a native
`<select>` is the better control, not least because a phone renders it with its own picker instead
of a dropdown and an on-screen keyboard.

Searching is `Hlibz.Redoc.AspNetCore` only. The control needs a stylesheet, and the adapters can
only reach their page through `HeadContent`.

## Where the controls sit

The document picker and the toolbar links are placed independently, and both default to the top
right:

```csharp
docs.PickerPlacement = ReDocToolbarPlacement.Sidebar;
docs.ToolbarItemsPlacement = ReDocToolbarPlacement.BottomRight;
```

| | |
|---|---|
| `TopRight` | Floating over ReDoc's content, in the corner. The default. |
| `BottomRight` | The other corner — clear of the page title and of ReDoc's own buttons above the content. |
| `Sidebar` | A row across the top of ReDoc's sidebar, above its search box. |

**Give both controls the same placement and they share one strip; give them different ones and each
gets a strip of its own.** In a corner they read *links first, then picker*: the links are
fixed-width and the picker grows with a document's name, so a row that ends with the widest element
doesn't shuffle as you switch documents. In the sidebar, which is a column, the picker leads —
you navigate with it, and a column reads top-down.

```
TopRight / BottomRight ─ [ Changelog ] [ Postman ] [ ◆ Widget Catalog ▾ ]
Sidebar ───────────────  [ ◆ Widget Catalog ▾ ]
                         [ Changelog ] [ Postman ]
```

`Sidebar` is the one to reach for once there are more documents than a pill in the corner can show,
or once their names are long enough to be cut off at 280px — there the picker takes the sidebar's
full width instead.

The sidebar is ReDoc's own React tree, so that placement hangs off `.menu-content` — the one class
ReDoc doesn't hash. Measured against 3.0.1: an element inserted as its first child survives
navigating the menu, switching color mode, opening and closing search, and scrolling. Two things
follow from the sidebar not being ours. ReDoc renders it after its bundle boots, so the strip starts
over the page and moves once the sidebar exists — a page where ReDoc never renders keeps a working
picker. And below ReDoc's own breakpoint the sidebar is hidden behind a floating button, so on a
phone that strip comes back out over the page rather than disappearing with it.

On a narrow screen every strip leaves its corner and stacks in the page flow above the content,
whatever its placement, so none of them covers the title — and so `BottomRight` stays off the
floating menu button a phone puts in exactly that corner. A bottom-right strip reads last wherever
it ends up.

Placement is `Hlibz.Redoc.AspNetCore` only.

## Remembering the last document

`docs.RememberLastDocument = true` makes `/{RoutePrefix}` send a returning viewer back to the
document they were last reading, instead of always to the first one.

It is off unless you ask for it, because it costs a cookie: `hlibz.redoc.document`, holding a
document name, set on each page. Some people would rather have documentation that sets no cookies
at all than have this. It's `HttpOnly`, `SameSite=Lax`, `Secure` over HTTPS, scoped to the route
prefix, and it expires after a year.

The cookie is a name to look up, never a path to follow, so a stale or hand-edited one falls back to
the first document rather than going anywhere of its own choosing. `Unlisted` documents aren't
remembered: an unlisted document is usually the gated one, and sending a viewer whose access has
lapsed to a page that answers 401 is worse than sending them to the public one.

## Named dark palettes

Three palettes ship as presets, for when you want a different dark mode rather than a different
accent:

```csharp
docs.ThemePreset = ReDocThemePreset.Nord;   // or Midnight, or GitHubDark
```

| | |
|---|---|
| `Midnight` | Near-black with a sky accent — this repo's ReDoc 2 dark theme as a ReDoc 3 palette, so a site moving off one of the adapters can keep looking like itself. |
| `Nord` | Polar Night surfaces, Snow Storm text, a Frost accent. |
| `GitHubDark` | GitHub's canvas and border greys with its blue accent. |

Each one is eleven greys and an accent, and that is a whole theme rather than a tint: **ReDoc 3's
dark mode is its grey ramp reversed**, and the page background, the panels, both border weights,
primary and secondary text and the code blocks all resolve to `var(--color-warm-grey-N)`. Point
those eleven values somewhere else and everything follows. (This is also why presets are a ReDoc 3
feature and the adapters don't have one: on ReDoc 2 a theme could only reach the keys the theme
object exposed.)

A preset paints **dark mode only**. All three are dark palettes, and a light counterpart invented
here would be neither the preset's nor ReDoc's — so set `DefaultColorMode = ReDocColorMode.Dark` if
a first-time viewer on a light OS should see it too. Setting `PrimaryColor` or
`DarkModePrimaryColor` takes the accent over from the preset, and `AdditionalCss` comes after the
preset and can reach the rest. Printing puts ReDoc's light ramp back, and brings the preset's own
paper-legible shade of its accent with it.

## Brand color, and anything else CSS can reach

The accent is a property:

```csharp
docs.PrimaryColor = "#c026d3";
docs.DarkModePrimaryColor = "#e879f9";   // optional; a hue picked for a white page is
                                         // usually too dark on a near-black one
```

Any CSS color works — hex, `rgb()`, `oklch()`, a named color, or a `var()` pointing at your own
design tokens. `DarkModePrimaryColor` is named for the color mode and not for a shade: unlike the
adapters' `DarkThemeOptions.PrimaryColorDark`, it is the whole accent while `html.dark` is in
effect, not a darker version of one.

Each property writes **two** variables, because ReDoc's accent isn't a single token:
`--color-primary-base` is the root one and derived tokens fall back to it (primary buttons, link
underlines, deep-link icons all follow it), but link *text* reads `--link-color-primary`, which has
a default of its own. That pair is the whole reason this is a property rather than a line of CSS —
finding the second name took a session in devtools.

For anything else, ReDoc 3 is themed entirely through CSS custom properties — there is no `theme`
config object any more — so you set variables yourself:

```csharp
docs.AdditionalCss = """
    :root     { --bg-color: #fffdf7; }
    html.dark { --bg-color: #0b0b0f; }
    """;
```

That CSS is appended to the page's stylesheet, which is served as a file like everything else, and
it comes **after** the accent above, so it wins at equal specificity. This package's own controls
(the toolbar and the picker) read ReDoc's variables rather than carrying a palette, so they follow
both the color mode and your accent color without being told.

There are around 2,900 of these variables and no published map of them, so for anything beyond the
accent, inspect the element in devtools and override the variable it actually reads.

A color value lands in a stylesheet, so `PrimaryColor` is checked when you set it: anything that
could end the declaration or open a new rule throws there rather than serving a page with a broken
stylesheet. It is not a color parser, though — a value CSS doesn't understand is simply ignored by
the browser, the same as any other typo in CSS.

## Authorization, caching and anything else endpoints can carry

`MapReDocDocuments` returns a convention builder over **every** endpoint it added — the pages, their
stylesheet, their scripts and the shared bundle:

```csharp
app.MapReDocDocuments(documents).RequireAuthorization("InternalDocs");
```

This is the reason to map endpoints rather than add middleware. With the existing hosts the page is
middleware, which carries no metadata, so there is nowhere for a policy to attach.

For the more common shape — one public document, the rest not — gate them one at a time with
`ForDocument`, and mark the gated ones `Unlisted` so the public page's picker doesn't offer them:

```csharp
ReDocDocumentsConventionBuilder docs = app.MapReDocDocuments(
    [
        new ReDocDocument("public", "Public API"),
        new ReDocDocument("internal", "Internal API", Unlisted: true),
    ]);

docs.ForDocument("internal").RequireAuthorization("Employees");
```

`ForDocument` covers that document's page, stylesheet and scripts — the init script carries the spec
URL, so gating the markup alone wouldn't be worth much — and **not** the shared ReDoc bundle. The
bundle is one endpoint for the whole site, so gating it with a single document would stop every
other page rendering, and it's ReDoc's own published JavaScript rather than anything of yours. An
unknown document name throws rather than silently protecting nothing.

`Unlisted` is a separate, deliberate step, because the two are not the same thing:

- **`RequireAuthorization` protects the page. `Unlisted` only hides the link.** An unlisted document
  is still served to anyone who knows its URL. Use both, not one.
- An unlisted document is left out of the picker on *listed* pages only. Unlisted pages list
  everything, so someone who can reach one can still navigate — and if a public page ends up with
  nowhere to go, it gets no picker at all rather than a dropdown holding only itself.
- The picker can't be audience-aware, because each page is built once at startup and served as a
  fixed string to everyone. Deciding by the page asking rather than the viewer asking is what keeps
  that true; see [the CSP section](#content-security-policy) for why nothing here is per-request.

For an app that isn't using endpoint routing there's `app.UseReDocDocuments(documents, ...)`, which
serves the same pages and takes the same options. Nothing can attach a policy to it, so `Unlisted`
is all you get there.

## The bundle, and how it's served

| | |
|---|---|
| ReDoc | 3.0.1, MIT, checked into this repo pre-compressed and embedded in the assembly |
| Served as | the stored bytes for whichever encoding the request accepts — nothing is compressed or decompressed per request |
| Size on the wire | **507 KB** brotli, 634 KB gzip, 2.2 MB for a client that takes neither |
| Caching | `Cache-Control: public, max-age=31536000, immutable`, `Vary: Accept-Encoding`, and a strong ETag per encoding, so a revalidation is a 304 |
| URL | `{RoutePrefix}/_redoc/redoc.standalone.js`, once for the whole site — switching documents re-downloads nothing |

Both encodings are built at quality 11 before the file is ever checked in, which is affordable
exactly because the bundle is fixed for the life of a package version. Response-compression
middleware can't do that: it pays a much cheaper setting, over and over, on every cache miss. If
you have such middleware in the pipeline it has nothing to do here — the response already carries a
`Content-Encoding`.

Brotli is preferred wherever the request accepts it, and `Accept-Encoding` is parsed rather than
searched, so `br;q=0` means what it says. The ETag differs per encoding because these are different
representations of one resource.

For comparison: Redoc.AspNetCore sends its bundle uncompressed with no `Cache-Control` at all, and
Swashbuckle sends `max-age=0`.

No Node and no network are involved in building this package: the bundle is a checked-in file.
[`assets/README.md`](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/src/Hlibz.Redoc.AspNetCore/assets/README.md)
records the version, the npm tarball's published SHA-512, and how to upgrade it. ReDoc's own MIT
licence is packed alongside as `THIRD-PARTY-NOTICES.txt`.

ReDoc 3's telemetry is **off**: the generated config always carries `disableTelemetry: true` unless
you set `EnableTelemetry = true`. A documentation page served out of someone's own app shouldn't
call a third party unless they asked for it.

## Content Security Policy

One call serves the pages under the policy they are known to work under:

```csharp
app.MapReDocDocuments(documents, docs => docs.UseDefaultContentSecurityPolicy());
```

That is the policy the pages are actually served under in
[`samples/Sample.Own`](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/samples/Sample.Own/Program.cs),
so the sample proves it rather than this README asserting it. Every allowance in it beyond
`default-src 'self'` is there because a blocked request said so:

```
default-src 'self';
script-src 'self';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https://cdn.redoc.ly;
font-src 'self' data:;
worker-src 'self' blob:;
child-src 'self' blob:;
connect-src 'self';
object-src 'none';
base-uri 'none'
```

It is sent with the **pages** and nothing else: a policy on the stylesheet or the scripts they load
changes nothing, because the page's own policy already decides what the page may load. A policy
your own middleware has already set is left alone — yours covers the whole app and should be the
one that holds — so this is for the case where these pages are the only ones with a policy, not a
way to override one.

Set `ContentSecurityPolicy` yourself for anything else; `ReDocDocumentsOptions.DefaultContentSecurityPolicy`
is the string above, to start from. Two allowances in it depend on things outside this package:

- **`connect-src 'self'`** assumes the spec is served from this origin. A spec elsewhere needs that
  origin added, or ReDoc can't fetch it.
- **`img-src … https://cdn.redoc.ly`** is the "API docs by Redocly" badge, which ReDoc's own bundle
  fetches from there. Drop it and the badge quietly removes itself (the bundle hides the image when
  it fails to load) — the words "API docs by" remain, and no third-party request is made.

Two things worth knowing:

- **No nonce, anywhere.** `script-src 'self'` is enough because nothing is inline: the ReDoc config,
  the color-mode bootstrap, the picker and all CSS are served as their own files. That is the whole
  reason to own the page template.
- **`style-src 'unsafe-inline'` is not negotiable here.** ReDoc renders through styled-components,
  which injects stylesheet rules at runtime. Handing it a nonce via `window.__webpack_nonce__` does
  not reach them on ReDoc 3 — measured, the page dies with styled-components error #17 and renders
  nothing. So a nonce-only `style-src` is not an option on this package, and no amount of owning
  the template changes that.

## Options

| `ReDocDocumentsOptions` | |
|---|---|
| `DefaultColorMode` | What a first-time viewer sees: `System` (default), `Light` or `Dark`. |
| `Favicon` | `href` for the page's `<link rel="icon">`. Unset, browsers ask for `/favicon.ico`. |
| `PrimaryColor` | The accent, as any CSS color. Writes `--color-primary-base` and `--link-color-primary` under `:root`. |
| `DarkModePrimaryColor` | The accent while `html.dark` is in effect, when it should differ. Defaults to `PrimaryColor`. |
| `ThemePreset` | A named dark palette: `None` (default), `Midnight`, `Nord` or `GitHubDark`. Dark mode only; either accent property above takes its accent over. |
| `SearchablePicker` | Make the picker a searchable list, filtering on display name and `Group`. Defaults to `false`. |
| `PickerPlacement` | Where the document picker sits: `TopRight` (default), `BottomRight` or `Sidebar`. |
| `ToolbarItemsPlacement` | The same, for the toolbar links, and set independently. Matching the picker's puts both in one strip. |
| `AdditionalCss` | Appended to the page's stylesheet, after the accent and the preset. |
| `ContentSecurityPolicy` | Sent with each page. Unset, none is sent; `UseDefaultContentSecurityPolicy()` sets the verified one. A policy your own middleware already set is left alone. |
| `PrintStyles` | Print the page light, without the toolbar. Defaults to `true`. |
| `AddToolbarItem(label, url, newTab)` | Adds a link button to the toolbar, beside the picker. `ToolbarItems` is the list behind it. |
| `RememberLastDocument` | `/{RoutePrefix}` returns you to the document you were last reading. Defaults to `false`; sets a cookie when on. |
| `HideDownloadButton` | Hide ReDoc's "Download OpenAPI description" button. |
| `RequiredPropsFirst` | List required properties before optional ones in schemas. |
| `EnableTelemetry` | Defaults to `false`, which emits `disableTelemetry: true`. |
| `AdditionalConfigJson` | Escape hatch for ReDoc options this package doesn't surface, e.g. `"""{"showExtensions": true}"""`. Parsed when you set it, so a malformed value throws at startup instead of blanking the page. |

Plus `RoutePrefix`, `RedirectToFirstDocument`, `SpecUrlTemplate`, `ValidateDocumentNames` and
`Links`, which are the same on all three packages and
[documented below](#multiple-openapi-documents).

## Moving from ReDoc 2: deep links change

ReDoc 3 is not a drop-in for ReDoc 2 in one respect that will bite you if anyone has bookmarks.
ReDoc 2 renders one long scrolling page with anchors like `#tag/widgets/operation/GetWidget`.
ReDoc 3 renders a page per tag and operation behind a hash router, so that old anchor renders
ReDoc 3's own "No page at this address" instead of the operation. Verified, not inferred.

What ReDoc 3 adds, beyond dark mode and CSS-variable theming: a real search index, OpenAPI 3.2,
AsyncAPI and GraphQL support, and "Copy / Open in ChatGPT / Open in Claude" actions on every page.
Try-it is a Redocly commercial feature on both versions.

---

# The dark theme, for Redoc.AspNetCore and Swashbuckle

For apps already serving ReDoc through one of those two packages. Everything in this half applies to
both adapters identically — the package reference differs, the code doesn't.

## What it fixes

[ReDoc](https://github.com/Redocly/redoc) 2's own `theme` config object covers most of its UI. A few
things are hardcoded to light-theme colors outside it, though, and stay light no matter what the
`theme` config says:

- The scrollbar, text selection, and the small section captions above "Query Parameters" /
  "Request Body schema" / "Response Schema".
- Every other level of a nested schema (e.g. an object nested inside an object nested inside an
  object). ReDoc alternates nested-schema backgrounds between the theme's `schema.nestedBackground`
  and a hardcoded `#ffffff`, so a deeply nested request/response schema flashes white partway down.
- The selected tab in the request/response sample panel (e.g. "Payload"). Its selected-state style
  pairs the theme's own light foreground color with a light background, so the label is invisible.

All of these are still present in 2.5.4, the newest ReDoc either host ships, so none of the
overrides is dead weight. This package fixes them with a small `<style>` override, on top of a full
dark `theme` config (colors, sidebar, right panel, code blocks, typography, schema).

## Quick start

```csharp
app.UseReDoc(options =>
{
    options.SpecUrl("/openapi/v1.json");   // on Swashbuckle: options.SpecUrl = "/openapi/v1.json";
    options.UseDarkTheme();
});
```

That's it for the zero-config path. To customize the accent color:

```csharp
options.UseDarkTheme(theme =>
{
    theme.PrimaryColor = "#a78bfa"; // links, active sidebar items, the GET badge, etc.
    // theme.PrimaryColorLight / theme.PrimaryColorDark — optional, computed if omitted
    // theme.AdditionalCss = ".my-rule { ... }"; // appended after the built-in overrides
});
```

Everything outside the accent color (success/warning/error colors, grays, backgrounds, etc.) is
intentionally fixed — this is a themed package, not a general theming engine.

## Let viewers choose: Light/System/Dark selector

Not everyone on your team wants dark docs. Enable the selector and each viewer gets a small
Light/System/Dark toggle in the top-right corner. Light is ReDoc's stock theme, exactly as it
renders without this package. System follows the viewer's OS setting (`prefers-color-scheme`) and
switches live when the OS setting changes:

```csharp
options.UseDarkTheme(theme => theme.EnableThemeSelector(defaultTheme: ReDocTheme.System));
```

`defaultTheme` (defaults to `Dark`) is what viewers see until they pick one. Their choice is then
remembered in the browser's `localStorage`. Switching re-renders ReDoc in place and keeps the scroll
position.

With the selector enabled, the dark CSS overrides are scoped under `html[data-redoc-theme="dark"]`.
`AdditionalCss` is not scoped and applies to both themes, so prefix your rules with that selector if
they should be dark-only.

## Fonts: no request to Google

ReDoc 2's page pulls Montserrat and Roboto from `fonts.googleapis.com` at runtime.
`UseSystemFonts()` renders the docs in the viewer's own OS fonts instead and removes that `<link>`
from the page, so it makes no third-party request at all:

```csharp
options.UseDarkTheme(theme => theme.UseSystemFonts());
```

That's one `style-src`/`font-src` entry you no longer need in a strict CSP, docs that render the
same on an air-gapped network, and no viewer IP addresses reaching Google — which LG München I ruled
a GDPR violation in 2022 (3 O 17493/20) when it happens without consent.

Name your own stack instead, or mix and match:

```csharp
options.UseDarkTheme(theme =>
{
    theme.UseSystemFonts();                          // sets all three, and drops the <link>
    theme.FontFamily = "Inter, system-ui, sans-serif";
    theme.HeadingsFontFamily = "Lora, Georgia, serif";
    theme.CodeFontFamily = "Fira Code, ui-monospace, monospace";
    // theme.LoadGoogleFonts = false;                // drop the <link> without changing the fonts
});
```

Self-hosted or bundled fonts go in `AdditionalCss` as `@font-face` rules, and the family name then
goes in the stacks above. Keep stacks to plain ASCII names: ReDoc's page carries its theme config
inside a JavaScript string literal, so a character that has to be escaped there breaks the whole
page. Quotes are allowed and dropped (CSS doesn't need them for a family name); anything else
unusable throws at startup rather than at runtime.

Dropping the `<link>` rewrites ReDoc's `index.html`, so if you set your own `IndexStream`, set it
before calling `UseDarkTheme`.

ReDoc 3 uses a system font stack by default and loads no web font, so none of this exists in
`Hlibz.Redoc.AspNetCore`.

## Favicon

ReDoc's page declares no icon, so browsers fall back to `/favicon.ico`. To point it somewhere else:

```csharp
options.UseDarkTheme(theme => theme.Favicon = "/favicon.svg");
```

Use an absolute URL or a root-relative path — the pages are served at `/{route}/index.html`, so a
relative one would resolve against the page's own folder.

## Printing

Dark docs print badly: the page is either a black rectangle or near-white text on white paper,
depending on the browser's "background graphics" setting. `UseDarkTheme` therefore ships a print
stylesheet that prints the documentation black-on-white and leaves out the sidebar and this
package's own controls. Turn it off with `theme.PrintStyles = false`.

It only changes colors and hides page furniture. Sections that are collapsed on screen stay
collapsed on paper — ReDoc doesn't render their content until they're opened, so CSS can't reach it.

## Content Security Policy

If your site doesn't send a strict `Content-Security-Policy` header, skip this section.

These adapters add inline tags through `ReDocOptions.HeadContent`: a `<style>` for the dark theme, a
`<style>` and `<script>` for the theme selector, and a `<style>` and `<script>` for the picker on
every `UseReDocDocuments` page. A strict policy blocks inline tags without a nonce. The nonce recipe
in the Redoc.AspNetCore README doesn't reach these tags, because it rewrites the `IndexStream`
template and the middleware only fills in `%(HeadContent)` afterwards. That recipe also misses the
host's own inline `<script>` that calls `Redoc.init`, so under a strict policy the page stays blank
even without this package.

Start from a policy that works on both hosts:

```
default-src 'self';
script-src 'self' 'nonce-{nonce}';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https://cdn.redoc.ly;
font-src 'self' data:;
worker-src 'self' blob:;
object-src 'none';
base-uri 'none'
```

Then fill in `HeadContent` yourself, so this package's tags get the nonce too:

```csharp
builder.Services.AddHttpContextAccessor();
// ...
IHttpContextAccessor accessor = app.Services.GetRequiredService<IHttpContextAccessor>();

app.UseReDoc(options =>
{
    options.SpecUrl("/openapi/v1.json");
    options.UseDarkTheme();

    Func<Stream> original = options.IndexStream;
    options.IndexStream = () =>
    {
        string nonce = GetNonce(accessor.HttpContext!); // however your app issues its CSP nonce
        using StreamReader reader = new(original());

        // Fill in HeadContent before the middleware does, so its tags get the nonce too.
        string html = reader.ReadToEnd().Replace("%(HeadContent)", options.HeadContent);

        // Add the nonce to every inline <script>. Tags with src= are covered by 'self'.
        html = Regex.Replace(
            html, @"<script\b(?![^>]*\bsrc=)", m => $"{m.Value} nonce=\"{nonce}\"");

        return new MemoryStream(Encoding.UTF8.GetBytes(html));
    };
});
```

With `UseReDocDocuments`, put the same code in `ConfigureReDoc`. The picker's tags are appended to
`HeadContent` after `ConfigureReDoc` runs, but the `IndexStream` above reads `HeadContent` per
request, so they get the nonce too.

Unless you call `UseSystemFonts()`, also allow `https://fonts.googleapis.com` in `style-src` and
`https://fonts.gstatic.com` in `font-src`, or ReDoc's font request is blocked.

### Why `style-src` keeps `'unsafe-inline'`

ReDoc renders through styled-components, which injects stylesheet rules at runtime. Whether a nonce
reaches those rules turns out to depend on the ReDoc version your host embeds, which is not
something you control:

| `style-src 'self' 'nonce-…'`, with `window.__webpack_nonce__` set | Result |
|---|---|
| Redoc.AspNetCore (ReDoc 2.0.0) | Renders. Two static inline styles are still blocked; allow them by hash: `'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='` and `'sha256-DLDPR1ic47WIdK2WyeLkblb/tm2mQH+Jt/NNhZWu1k0='` |
| Swashbuckle.AspNetCore.ReDoc (ReDoc 2.5.4) | **Blank page.** styled-components error #17 — the nonce never reaches it, and 2.5.4's bundle doesn't contain `__webpack_nonce__` at all |

Both measured in a browser, against the policy above with `style-src` narrowed to
`'self' 'nonce-…'`. So `'unsafe-inline'` in `style-src` is the portable answer, and the one this
README recommends; the nonce is still worth having on `script-src`, which is where it does real
work. If you're on Redoc.AspNetCore and want the stricter variant, it does work today — just know
that it stops working the moment that host updates its bundle.

Note that a `style-src` listing a nonce or a hash makes browsers **ignore** `'unsafe-inline'` in the
same directive, so these are two recipes, not two halves of one.

---

# Multiple OpenAPI documents

One ReDoc page per OpenAPI document, each with a picker in the top-right corner for switching
between them. This works the same on all three packages; only the mapping call differs.

On `Hlibz.Redoc.AspNetCore`:

```csharp
builder.Services.AddOpenApi("widgets");
builder.Services.AddOpenApi("manufacturers");

// ...

app.MapOpenApi();
app.MapReDocDocuments(
    [
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers"),
    ],
    docs => docs.DefaultColorMode = ReDocColorMode.System);

app.MapGet("/widgets", GetWidgets).WithGroupName("widgets");
app.MapGet("/manufacturers", GetManufacturers).WithGroupName("manufacturers");
```

On either adapter, `app.UseReDocDocuments(...)` with the same two arguments, and the theme goes on
through `ConfigureReDoc`:

```csharp
app.UseReDocDocuments(
    [
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers"),
    ],
    docs =>
    {
        docs.ConfigureReDoc = (redoc, _) =>
            redoc.UseDarkTheme(theme => theme.EnableThemeSelector(defaultTheme: ReDocTheme.System));
    });
```

Both serve `/api-docs/widgets/` and `/api-docs/manufacturers/`, with `/api-docs` redirecting to the
first. Each document reads its spec from `/openapi/{documentName}.json`, which is where
`MapOpenApi()` serves it.

Tag every endpoint with `.WithGroupName(...)`. By default, an endpoint without a group name appears
in every document.

## Grouping documents in the picker

Give documents a `Group` and the picker puts them under a heading, as an `<optgroup>` — what you
want once there are several versions of one API:

```csharp
app.MapReDocDocuments(
    [
        new ReDocDocument("overview", "Overview"),
        new ReDocDocument("payments-v1", "Payments v1", Group: "Payments"),
        new ReDocDocument("payments-v2", "Payments v2", Group: "Payments"),
    ]);
```

Documents keep the order you give them, and a group appears where its first member does, so grouped
and ungrouped ones mix freely — `Overview` stays at the top of the list above. The heading is a
label: it isn't selectable and it changes no route. This works on all three packages.

On `Hlibz.Redoc.AspNetCore` a group name is also searchable — see
[`SearchablePicker`](#searching-the-picker), where typing `payments` brings up both versions above.

| `ReDocDocument` | |
|---|---|
| `Name` | The OpenAPI document name, as passed to `AddOpenApi(name)`. |
| `DisplayName` | Label in the picker and the browser tab. Defaults to `Name`. |
| `SpecUrl` | This document's spec URL, if it isn't at `SpecUrlTemplate`. |
| `Route` | The page's path, e.g. `"reference/v2/taxes"`. Defaults to `{RoutePrefix}/{Name}`. |
| `Unlisted` | Keep this document out of the picker on listed documents' pages. Hides the link; protects nothing. |
| `Group` | Heading this document sits under in the picker, as an `<optgroup>`. |

| `ReDocDocumentsOptions`, the members every package has | |
|---|---|
| `RoutePrefix` | Where pages go by default. Defaults to `"api-docs"`. |
| `RedirectToFirstDocument` | Redirect `/{RoutePrefix}` to the first document. Defaults to `true`. |
| `SpecUrlTemplate` | Defaults to `"/openapi/{documentName}.json"`. |
| `ValidateDocumentNames` | Check at startup that every name is a registered document. Defaults to `true`. |
| `Links` | Plain entries listed after the documents, e.g. `new ReDocLink("Swagger UI", "~/swagger")`. |
| `ConfigureReDoc` | **Adapters only.** Runs for every page's `ReDocOptions`: theme, extra `HeadContent`, and so on. The host has no `ReDocOptions` to hand you — its page settings are properties on the options object instead. |

Picker links are relative, so they keep working under a `PathBase` or behind a reverse proxy. A link
URL that starts with `~/` is resolved the same way, against the app's base path. Other URLs are used
as-is.

To move a page, set its `Route`. Don't change `RoutePrefix` inside `ConfigureReDoc`: the pickers on
the other pages would link to the old path, so it throws if you do.

## Mistyped document names fail at startup

The list above has to mirror your `AddOpenApi(name)` calls. When the two drift, the page itself
still serves fine and only its spec request 404s — so the app starts clean, the server log says
nothing, and the mistake surfaces only when someone opens the page and finds ReDoc's "Something went
wrong…". The names are checked at startup instead, and it throws:

```
UseReDocDocuments was given OpenAPI document names that aren't registered:
  - 'wigets': page '/api-docs/wigets/' would load an empty spec from '/openapi/wigets.json'.
Registered documents: 'facilities', 'manufacturers', 'widgets'.
```

Both `AddOpenApi(name)` and Swashbuckle's `SwaggerDoc(name, ...)` are recognized, matched the same
way `MapOpenApi` matches them — case-insensitively. A document is only checked when its name
actually builds its spec URL, so a document with its own `SpecUrl`, or any document when
`SpecUrlTemplate` has no `{documentName}`, is left alone. Nothing is checked at all if the app
registers no OpenAPI document provider the check recognizes.

Set `ValidateDocumentNames = false` if your specs come from somewhere the check can't see, e.g.
static files served alongside documents you do register.

---

## Compatibility

| | `Hlibz.Redoc.AspNetCore` | The two adapters |
|---|---|---|
| Library target | `net8.0` | `net8.0` |
| Works in host apps targeting | net8.0, net9.0, net10.0 | net8.0, net9.0, net10.0 |
| Requires | nothing but ASP.NET Core | `Redoc.AspNetCore` ≥ 1.0.0, or `Swashbuckle.AspNetCore.ReDoc` ≥ 10.3.1 |
| ReDoc version | 3.0.1, embedded in this package | whatever the host embeds: 2.0.0 or 2.5.4 |
| Trimming / AOT | `IsAotCompatible`, and it holds end to end: nothing here serializes reflectively | `IsAotCompatible` for this assembly only — the host serializes ReDoc's config reflectively, which a NativeAOT app won't run |

Referencing both adapters from one project is a `CS0433` ambiguity: they declare the same public
type names in the same namespace on purpose, so the code you write doesn't change when you switch
hosts. Installing one is the supported case.

## Running the samples locally

Three samples, the same three OpenAPI documents, one per package:

```bash
dotnet run --project samples/Sample.Own               # :5499 — ReDoc 3, our own host
dotnet run --project samples/Sample.RedocAspNetCore   # :5299 — dark theme, ReDoc 2.0.0
dotnet run --project samples/Sample.Swashbuckle       # :5399 — dark theme, ReDoc 2.5.4
```

Each opens `/api-docs` automatically and redirects to the first document. Run them together to
compare: the two adapter samples have line-for-line identical `Program.cs` files, so anything that
differs between them is a host difference. `Sample.Own` sets the strict CSP from this README itself,
so the policy is proven by a running app.

## Why

When I started using ReDoc, the first thing I wanted was a dark theme. The original
[Redoc.AspNetCore](https://github.com/jonashendrickx/Redoc.AspNetCore) package doesn't ship one, and
I couldn't find another NuGet package that did.

So I asked Claude Code to build one for my private project. That worked because the authors of
Redoc.AspNetCore made its API very configurable: the theme config and `HeadContent` hooks were all
the AI needed to produce a proper dark ReDoc page.

Then I figured I'm probably not the only one who wants dark API docs. So I moved the theme into this
public repo and NuGet package for the community to use.

Next I needed several OpenAPI documents in one app. Swagger UI has a dropdown for that, but ReDoc
shows one spec per page. So the package got a document picker, and a broader name to go with it: it
used to be `Hlibz.Redoc.AspNetCore.DarkTheme`.

Then the ceiling showed up. Everything above is built out of hooks someone else's middleware chose
to expose, which meant no control over the bundle, the cache headers, the markup or the CSP — and
when ReDoc 3 shipped with dark mode built in, neither host moved. So the engine was split out of the
adapter, and `Hlibz.Redoc.AspNetCore` serves the page itself.

## Contributing

Found a bug or have an idea for a feature? Feel free to
[open an issue](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/issues) or a pull
request. You can also reach me on [LinkedIn](https://www.linkedin.com/in/hzinchenko/).

## License

MIT — see
[LICENSE](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/LICENSE).
ReDoc itself is MIT too; `Hlibz.Redoc.AspNetCore` carries its notice as `THIRD-PARTY-NOTICES.txt`.
