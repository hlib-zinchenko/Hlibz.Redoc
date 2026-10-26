# Design notes

Why things here are the way they are, and what was measured to decide. None of this is needed to
use the packages — the per-package READMEs are. It's here so a reader who wonders "why not just…"
gets the answer, and so a future change doesn't undo a decision whose reason has been forgotten.

## Why four packages

`Hlibz.Redoc.Extensions` is the host-agnostic engine. `Hlibz.Redoc.Extensions.RedocAspNetCore` and
`Hlibz.Redoc.Extensions.Swashbuckle` are adapters that wire it to each host's `ReDocOptions`.
`Hlibz.Redoc.AspNetCore` is a host of its own, embedding ReDoc 3 and owing neither of them
anything.

A package that extends one host's `ReDocOptions` type can never serve the other, and the adapters
have to be separate packages because NuGet has no optional dependencies: one package referencing
both hosts would make every consumer download two embedded ReDoc bundles. The split exists at all
because `Redoc.AspNetCore` is effectively unmaintained while Swashbuckle's ReDoc has roughly 300×
the downloads.

Both adapters declare the **same public type names** in the same namespace
(`ReDocDocumentsOptions`, `ReDocDocumentsBuilderExtensions`, `RedocOptionsExtensions`), so the code
a consumer writes is identical on either host — and so the conformance tests can be one set of
assertions compiled twice. The cost is that referencing both adapters from one project is a
`CS0433` ambiguity needing an `extern alias`. Installing one is the supported case; renaming the
types apart would be the wrong fix.

The namespace is `Hlibz.Redoc.Extensions` everywhere, core and adapters alike, so one `using` works
whichever package you installed. Assembly name and namespace deliberately differ for the adapters.

Lineage: `Hlibz.Redoc.AspNetCore.DarkTheme` → `Hlibz.Redoc.AspNetCore.Extensions` →
`Hlibz.Redoc.Extensions.RedocAspNetCore`. `UseDarkTheme()` must keep behaving as it did in all of
them.

## Where the line between the two products is

The adapters are the **dark theme for ReDoc 2**, which is what both existing hosts are frozen on
(2.0.0 and 2.5.4). ReDoc 3 themes itself through ~2,900 CSS custom properties and has dark mode, a
color-mode toggle and a system font stack built in — so on the ReDoc 3 host the dark theme, the
theme selector and the whole fonts feature would be inert or redundant, and `UseDarkTheme` is not
part of its surface at all.

What the ReDoc 3 host adds instead is everything the other hosts can't do: per-document
authorization through endpoint routing, a page with nothing inline, `immutable` cache headers, and
the picker.

## ReDoc 2's hardcoded colors

ReDoc 2's `theme` config covers most of its UI, but three things are hardcoded outside it: the
scrollbar and selection and the small section captions; alternating nested-schema backgrounds
(theme value, then a literal `#ffffff`, so a deeply nested schema flashes white); and the selected
tab in the sample panel, whose selected state pairs the theme's light foreground with a light
background, making the label invisible.

All three are still present in 2.5.4, so none of the CSS overrides is dead weight. Phase 2 probed
the computed colors (body, scrollbars, sidebar, `h5` captions, every selected tab, the
`--hlibz-control-*` tokens, the nested-schema wrappers) on both hosts and got byte-identical
output, so the theme renders the same on 2.0.0 and 2.5.4.

## The theme selector intercepts `window.Redoc`

A switch has to re-run `Redoc.init`, because ReDoc builds its theme once per store. The script
therefore intercepts the `window.Redoc` global as the bundle assigns it and returns a wrapped copy
— webpack exports are getter-only and can't be reassigned in place. The wrapped `init` drops the
dark `theme` config for Light, and a switch is `Redoc.destroy` plus a re-init.

With the selector on, the dark CSS is scoped under `html[data-redoc-theme="dark"]`; with it off,
the scope is plain `html`.

## Fonts: three constraints that look unrelated

1. The families go in ReDoc's `typography` theme config, but the selector drops that config for
   Light — so the script also carries a fonts-only theme (`lightThemeConfig`). Otherwise Light
   reverts to Montserrat/Roboto with nothing loading them.
2. Setting the families doesn't stop the request: the Google Fonts `<link>` sits above
   `%(HeadContent)` in the index template, so `UseDarkTheme` wraps `IndexStream` and strips the tag.
   That is why a caller's own `IndexStream` must be set first.
3. `Redoc.AspNetCore`'s page carries the config as `JSON.parse('%(ConfigObject)')` — a JS string
   literal that eats backslashes, so any `\uXXXX` the serializer emits breaks `JSON.parse` on the
   *whole* config: blank page, one console error. Font stacks are therefore stripped of quotes
   (optional in CSS) and throw on anything else needing escaping. Any future free-text theme setting
   has the same constraint.

Swashbuckle's page carries the config as raw JSON in a separate `index.js`, where the no-`\uXXXX`
rule buys nothing. It is kept anyway: a font stack that worked on one host and threw on the other
would be worse than one that is uniformly strict.

## CSP, measured per ReDoc version

Under `style-src 'self' 'nonce-…'` with `window.__webpack_nonce__` set:

| | Result |
|---|---|
| ReDoc 2.0.0 (`Redoc.AspNetCore`) | renders; two static inline styles blocked, allowable by hash |
| ReDoc 2.5.4 (Swashbuckle) | blank page, styled-components error #17 — the bundle contains no `__webpack_nonce__` at all |
| ReDoc 3.0.1 (our host) | blank page, same error |

So `style-src 'unsafe-inline'` is the portable answer and the nonce belongs on `script-src`. Don't
collapse that into one claim covering every host, and don't advertise nonce support for styles
anywhere.

`script-src 'self'` with no nonce is achievable, but only by owning the page template: that is the
entire reason `Hlibz.Redoc.AspNetCore` has its own `ReDocPage`. Nothing is inline there — not the
config, not the picker, not the color-mode bootstrap.

## The ReDoc 3 host

### Everything is worked out at startup

`ReDocSite` builds one `ReDocSitePage` per document, holding its finished markup and files. Nothing
is built per request, because nothing varies per request. That property is what makes the picker
page-based rather than viewer-based, what keeps `immutable` honest, and what forces per-document
authorization to be said on the document rather than on the returned convention builder — by the
time `MapReDocDocuments` returns, markup is frozen and spec endpoints are mounted.

### Authorization is carried on a derived record

The gated document is `ReDocGatedDocument`, **internal**, deriving from core's now-unsealed
`ReDocDocument`; the extension method returns the base type. That is what keeps authorization out
of the adapters by construction rather than by a runtime check — the type doesn't exist in a
package they reference, so their consumers cannot write it and nothing has to throw.

The cost: nothing anywhere may copy, rebuild or compare a `ReDocDocument`. A `with` expression or a
reconstruction from parts would silently drop the gating. It was verified that nothing does before
unsealing; keep it that way.

### Why gating implies site-wide spec serving

One gated document turns spec-serving on for **every** page in the call. The public pages can't be
left reading `MapOpenApi()`'s endpoint, since that one endpoint serves the gated document too — so
the one-call form would otherwise produce a site whose public pages have no spec at all, which is
the two-knob problem it exists to remove.

It throws rather than half-working in four places: a gated document with its own `SpecUrl`; a gated
document alongside a non-default `SpecUrlTemplate`; a gated name no provider offers; and any gated
document handed to the `UseReDocDocuments` middleware form, which carries no metadata to gate.

`ForDocument` covers one document's own endpoints and deliberately **not** the shared bundle — one
endpoint serves the whole site, so gating it with a single document would blank every other page,
and it is ReDoc's own JS. An unknown name throws: a `RequireAuthorization` that silently protects
nothing is the failure worth ruling out.

`MapReDocDocuments` returns a concrete `ReDocDocumentsConventionBuilder` rather than
`IEndpointConventionBuilder` so it can carry `ForDocument`. That widening was done before the first
v2 tag on purpose: source-compatible, binary-breaking, and free only while nothing is published.

### Spec generation

`OpenApiSpecSource` generates one document through
`Microsoft.Extensions.ApiDescriptions.IDocumentProvider.GenerateAsync(string, TextWriter)` — the
same hook `dotnet getdocument` uses, which is why one lookup covers `Microsoft.AspNetCore.OpenApi`,
Swashbuckle and NSwag. Resolution is by exact signature, not by name: .NET 10's provider carries
more than one `GenerateAsync`. The provider's *own* spelling of the document name is passed back to
it, since matching here is case-insensitive while Swashbuckle's lookup is ordinal.

Three decisions worth keeping: the provider is resolved per request (an implementation may be
scoped) while *which* provider is settled at startup; the document is buffered rather than streamed,
so a generator that throws half-way produces a 500 rather than a 200 carrying half a document; and
the spec is sent `no-store`, alone among the things this package serves, because a shared cache
holding a gated document would hand it on.

Output was diffed against `MapOpenApi()`'s endpoint: identical but for the assembly-name tag and the
port. `servers` comes from the server's own addresses rather than the request, unlike `MapOpenApi`'s
— an `AddDocumentTransformer` is the fix, and the README says so.

### `OpenApiDocumentNames`

The same interface, resolved by type name across loaded assemblies, with every
`GetDocumentNames()` unioned. The interface is internal in every implementation by design, so
reflection is the only option even on net9/net10 and multi-targeting wouldn't help. Returning
`null` rather than an empty set when no provider resolves is what keeps "can't verify" from
throwing; every reflection step is wrapped per assembly for the same reason.

### The color mode is stamped server-side

ReDoc 3 resolves the mode from `localStorage['redoc.colorMode']` first and `prefers-color-scheme`
last, and then sets the class *itself* — so stamping the markup alone gets overwritten and a
configured default does nothing for precisely the first-time viewers it is meant for. A configured
`Light` or `Dark` is therefore also recorded under that key when nothing is stored yet; `System`
writes nothing, so the page goes on following the OS. ReDoc does this part with an inline script,
which a strict CSP blocks, which is why `redoc-colormode.js` is served as a file.

### Dark presets are eleven greys

ReDoc 3's dark mode is its grey ramp reversed: every semantic token reads
`var(--color-warm-grey-N)` in both modes, and only those eleven values differ. So pointing eleven
variables somewhere else is a whole theme, which is why presets exist on this host and couldn't on
the adapters (where a theme could only reach the keys the theme object exposed), and why putting the
light ramp back is all the print stylesheet needs to do.

Setting `PrimaryColor` or `DarkModePrimaryColor` turns the preset's accent off rather than letting
it lose: the preset's selector is one class heavier than the `:root:root` a both-modes
`PrimaryColor` is written as, so ordering couldn't settle it. Each preset also carries the shade of
its accent that reads on paper, emitted beside the restored light ramp. Adding a preset is a row in
`PaletteOf` and an enum member.

### The accent is two variables

`--color-primary-base` is the root token and derived ones fall back to it, but link *text* reads
`--link-color-primary`, which has a default of its own. That pair is the whole reason the accent is
a property rather than a line of CSS — finding the second name took a session in devtools.

### The bundle

Checked in pre-compressed (`gzip -n`, or the file differs on every run), both encodings at quality
11, which is affordable exactly because the bundle is fixed for the life of a package version.
Response-compression middleware can't do that: it pays a much cheaper setting on every cache miss.
`assets/README.md` records the version, the npm tarball integrity and the upgrade steps — and it is
embedded into the test assembly, with every row of its table asserted against the shipped bundle, so
an upgrade that doesn't update the note fails a test rather than making the note a lie.

Served once for the whole site at `{RoutePrefix}/_redoc/`, imported by each page's `redoc-init.js`
through a relative href, so one cached copy serves every page and the URLs survive a path-rewriting
proxy.

`ReDocPage.ConfigJson` writes the config with `Utf8JsonWriter`, never `JsonSerializer`: a slim or
AOT host has reflection-based serialization off.

### Trailing slashes are one endpoint

A route pattern ignores its trailing slash, so `{route}/` and `{route}` are the same endpoint —
mapping both throws `AmbiguousMatchException`. The handler checks the path itself and 302s when the
slash is missing, because serving the page there would leave every relative href resolving one
folder up.

## The picker and the toolbar

### The adapters' output must stay byte-identical

`ReDocDocumentPicker` exposes its `Css` and `BuildScript` separately from `Build`, because the
adapters can only inline them through `HeadContent` while the ReDoc 3 host serves them as files. The
host-only additions — `<optgroup>` groups, `BuildScript`'s `spot`, and its `searchable`, which picks
a different template entirely — resolve to the previous text at their defaults, including the blank
lines they'd occupy, precisely so a diff of the adapters' served pages stays empty. That diff is the
standard any change in here should meet.

### Placement is one container per spot

`PickerPlacement` and `ToolbarItemsPlacement` place the two controls independently. The mechanism is
one container per spot, found by that spot's own class: matching placements resolve to the same
element at runtime and share a row, different ones each build their own. So placement hangs off
`ReDocToolbar.ScriptFunctionFor`, i.e. off *creating the container* — whichever script runs first
creates, the other appends to what it finds. `ReDocToolbarSpot.Shared` is the adapters' unplaced
container, and the reason their inlined script is still byte-identical.

Order inside a strip is CSS, never DOM order, because the two scripts append in load order and only
one of them may exist. The picker carries `order: -1` in core (it leads the theme selector on the
adapters, which must not move), so the host outweighs it by one class: a corner strip gets
`order: 1` on the picker — links first, since the picker is the element that grows with a document's
name and a row that ends with it doesn't shuffle — and `SidebarCss`, emitted last, puts `order: -1`
back for a column. Both are emitted only when they can matter, so the stylesheet says what the page
does.

### The sidebar hook

`.menu-content` is the one class ReDoc doesn't hash. Measured on 3.0.1: a node inserted as its first
child survives menu navigation, a color-mode switch, the search dialog and scrolling. Two things the
browser showed and nothing else would: ReDoc renders the sidebar after its bundle boots, so the
strip starts over the page and moves once a `MutationObserver` sees the sidebar; and below ReDoc's
own 50rem breakpoint the sidebar is `display: none` until its floating button opens it, so a
`matchMedia` listener brings the strip back out there. Dropping either fallback leaves a phone, or a
failed render, with no picker at all.

Under that same breakpoint core's base rule drops every strip into the page flow, which is also what
keeps `BottomRight` off the floating menu button a phone puts in that corner. DOM order is then
reading order, so the bottom-right strip inserts itself after every strip already at body level
while the others insert first.

### The searchable picker

A whole other control, so `ReDocDocumentPicker` holds a whole other template rather than
placeholders in the `<select>` one — the useful property being that the frozen path is impossible to
change from there. Off by default on purpose: a native `<select>` is better while the list fits on
screen, and a phone renders it with its own picker.

The popover is `position: fixed`, placed from the pill's rect in script, because ReDoc's sidebar is
`overflow-y: auto` and would clip an absolutely positioned list — and the sidebar is where a site
with enough documents to want search puts its picker. It flips above the pill when there isn't room
below, which is what `BottomRight` always is. Rows go in through `place` so grouping follows the
`<optgroup>` rule exactly (heading where the group's first member falls, later members joining it),
with the keyboard's list read back out of the DOM afterwards, in the order it ended up on screen.

**Anything hidden with the `hidden` attribute needs its own `[hidden] { display: none }` rule**,
because the attribute is backed by the browser's stylesheet and the `display` these carry on a class
of ours outranks it. The failure is not subtle but reads as something else entirely: the popover is
on screen from the moment it is built, covering its own pill, so the filter box is what you click,
`open` never runs, and nothing it attaches — positioning, Escape, outside-click — exists. It shipped
that way once, caught in a sample rather than by a test.

### `Unlisted` is about the page, never the viewer

A document is left out of the picker on *listed* pages; unlisted pages list everything. The rule is
about the page doing the asking because each page is built once at startup and served to everyone as
the same string — making it identity-aware would cost the whole "nothing is per-request" property
`ReDocSite` is built on. It is a visibility flag and **not** access control; say so wherever it is
documented, because a reader who assumes otherwise ships an open page.

## Core's internals are internal

Core's workhorses stay `internal`; the adapters, the host and the core test project reach them
through `InternalsVisibleTo` entries listed **by assembly name** — renaming any of those projects
means editing that list. This is deliberately not a public composition API: the public surface is
still only the options types a consumer writes, so nothing in there is a compatibility promise yet.
The cost is that a third party can't write their own adapter. "Core emits a model with two
renderers" is the eventual replacement; don't build it before something needs it.

## Documentation layout

Each published package packs its own README, because nuget.org shows one package's page at a time
and in-page anchors don't resolve there. The root `README.md` is the GitHub face: it chooses a
package and compares them, and links out for the rest. Rationale lives here, in this file, so the
READMEs can stay about what a reader has to do.
