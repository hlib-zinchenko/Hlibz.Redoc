# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Add-ons for ReDoc UI middleware: a dark theme (a CSS string plus a ReDoc theme config dictionary),
an optional Light/System/Dark theme selector, and a document picker that serves several OpenAPI
documents as ReDoc pages with a dropdown between them.

As of v2 this is **four packages**, not one. `Hlibz.Redoc.Extensions` is the host-agnostic engine;
`Hlibz.Redoc.Extensions.RedocAspNetCore` and `Hlibz.Redoc.Extensions.Swashbuckle` are the adapters
that wire it to [Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore)'s and
[Swashbuckle.AspNetCore.ReDoc](https://www.nuget.org/packages/Swashbuckle.AspNetCore.ReDoc)'s
`ReDocOptions`; `Hlibz.Redoc.AspNetCore` is our own host, which embeds ReDoc itself and owes
neither of them anything. The adapter split exists because `Redoc.AspNetCore` is effectively
abandoned while Swashbuckle's has ~300× the downloads, and a package that extends one host's
`ReDocOptions` type can never serve the other. Adapters have to be separate packages: NuGet has no
optional dependencies, so one package referencing both hosts would make every consumer download two
embedded ReDoc bundles.

**The two products, and where the line is.** The adapters are the *dark theme* for ReDoc 2.x, which
is what both existing hosts are frozen on (2.0.0 and 2.5.4). Our own host ships **ReDoc 3**, which
themes itself through ~2,900 CSS custom properties and has dark mode, a color-mode toggle and system
fonts built in — so the dark theme, the theme selector and the whole fonts feature are inert or
redundant there, and `UseDarkTheme` is not part of its surface at all. What the host adds instead is
everything the hosts can't do: per-document authorization through endpoint routing — one call that
covers the page, the spec and the link to it — a page with nothing inline, `immutable` cache
headers, and the picker. `V2-PHASE3.md` records how that was decided and
what was measured; don't reintroduce the theme to the host without reading it.

Lineage, and the compatibility it obliges: `Hlibz.Redoc.AspNetCore.DarkTheme` →
`Hlibz.Redoc.AspNetCore.Extensions` → `Hlibz.Redoc.Extensions.RedocAspNetCore`. `UseDarkTheme()`
must keep behaving as it did in all of them. Namespace is `Hlibz.Redoc.Extensions` for everything,
core and adapters alike, so a consumer writes one `using` whichever adapter they installed — the
assembly name and the namespace deliberately differ for the adapters.

Both adapters declare the **same public type names** in that namespace (`ReDocDocumentsOptions`,
`ReDocDocumentsBuilderExtensions`, `RedocOptionsExtensions`), so the code a consumer writes is
identical on every host and `tests/Conformance` can be plain shared source. The cost: referencing
both adapter packages from one project is a `CS0433` ambiguity needing an `extern alias`. Installing
one adapter is the supported case; don't "fix" this by renaming types apart.

## Requires

* .NET 10 SDK (the library itself targets `net8.0`; the sample targets `net10.0` to use the native `AddOpenApi()`/`MapOpenApi()` APIs, which require net9.0+)

## Commands

```bash
# Build everything
dotnet build

# Run the tests — four suites: core, the shared conformance source per adapter, then our host
dotnet test --project tests/Hlibz.Redoc.Extensions.Tests
dotnet test --project tests/Hlibz.Redoc.Extensions.RedocAspNetCore.Tests
dotnet test --project tests/Hlibz.Redoc.Extensions.Swashbuckle.Tests
dotnet test --project tests/Hlibz.Redoc.AspNetCore.Tests

# Run the sample apps (each opens /api-docs automatically, see launchSettings.json)
dotnet run --project samples/Sample.RedocAspNetCore   # :5299
dotnet run --project samples/Sample.Swashbuckle       # :5399
dotnet run --project samples/Sample.Own               # :5499

# Pack all four packages locally
dotnet pack src/Hlibz.Redoc.Extensions -c Release -o ./nupkg
dotnet pack src/Hlibz.Redoc.Extensions.RedocAspNetCore -c Release -o ./nupkg
dotnet pack src/Hlibz.Redoc.Extensions.Swashbuckle -c Release -o ./nupkg
dotnet pack src/Hlibz.Redoc.AspNetCore -c Release -o ./nupkg
```

Test projects use Microsoft Testing Platform (`test.runner` in `global.json`), so `dotnet test`
needs `--project <path>` rather than a bare directory argument — which is also why there is no
one-command "run all tests": each suite gets its own invocation, in CI too. `dotnet pack` also
takes one project at a time.

## Architecture

- `src/Hlibz.Redoc.Extensions.RedocAspNetCore/` — one of the two adapters, and the whole
  host-specific surface: three files holding the two public entry points,
  `RedocOptionsExtensions.UseDarkTheme(this ReDocOptions, Action<DarkThemeOptions>?)` and
  `ReDocDocumentsBuilderExtensions.UseReDocDocuments(this IApplicationBuilder, ...)`, plus
  `ReDocDocumentsOptions : ReDocDocumentsOptionsBase` adding the one genuinely host-typed member,
  `ConfigureReDoc` (`Action<ReDocOptions, ReDocDocument>`). Deriving the options rather than
  generalizing them keeps the public type name a consumer writes as `ReDocDocumentsOptions` on
  every host. `UseReDocDocuments` is ~25 lines: build options, call `ReDocDocumentsPlan.Build`,
  loop its pages calling `UseReDoc`. The `ArgumentNullException.ThrowIfNull` pair stays here rather
  than moving into `Build`, so the null checks still run before a caller's `configure` does.
- `src/Hlibz.Redoc.Extensions.Swashbuckle/` — the same three files against
  `Swashbuckle.AspNetCore.ReDoc` 10.3.1, which bundles redoc **2.5.4** where `Redoc.AspNetCore`
  bundles 2.0.0. Only two lines differ from the other adapter, and both are commented in the code:
  `SpecUrl` is a property here (`redoc.SpecUrl = ...`) rather than a method, and `IndexStream` is
  never null, so `UseDarkTheme`'s wrap is guarded on `LoadGoogleFonts` alone. Two things that look
  like they should differ and don't: this host resolves `IOptionsSnapshot<ReDocOptions>` from a
  scope of its own per `UseReDoc` call, so a page per document is safe — go through the setup-action
  overload rather than `UseReDoc(options)`, so a caller's `services.Configure<ReDocOptions>` still
  applies — and its prefix redirect is 301 to a relative `index.html`, the same as the other host's.
  - Its page carries the config as `Redoc.init(%(SpecUrl), %(ConfigObject), ...)` in a separate
    `index.js`: raw JSON, not a JS string literal, so `ReDocFonts`' no-`\uXXXX` rule buys nothing
    here. **Keep it anyway** — a font stack that worked on one host and threw on the other would be
    worse than one that is uniformly strict.
  - A third host difference, in behaviour rather than in code: under `style-src 'self' 'nonce-…'`
    with `window.__webpack_nonce__` set, redoc **2.0.0 renders** (only two static inline styles are
    blocked, and the README gives their hashes) while **2.5.4 dies** with styled-components error
    #17 — its bundle doesn't contain `__webpack_nonce__` at all. Both measured in a browser. The
    README therefore recommends `style-src 'unsafe-inline'` as the portable answer and puts the
    nonce on `script-src` only; don't collapse that back into one claim covering both hosts.
- `src/Hlibz.Redoc.AspNetCore/` — **our own host**: it embeds ReDoc and serves its own page, so it
  references no host package, only core. Public surface is `MapReDocDocuments` (endpoint routing,
  returning a `ReDocDocumentsConventionBuilder` over every endpoint it adds, so
  `.RequireAuthorization()` reaches the whole page) plus a `UseReDocDocuments` middleware form for
  an app without routing, and `ReDocDocumentsOptions : ReDocDocumentsOptionsBase` — same type name
  as the adapters', different members, because this host has no `ReDocOptions` to hand to a
  `ConfigureReDoc` callback.
  - `MapReDocDocuments` returns a **concrete class, not `IEndpointConventionBuilder`**, so it can
    carry `ForDocument(name)`. That return type was widened before the first v2 tag on purpose:
    source-compatible, binary-breaking, and free only while nothing is published.
    `ForDocument` covers one document's own endpoints and deliberately **not** the shared bundle —
    one endpoint serves the whole site, so gating it with a single document would blank every
    other page, and it is ReDoc's own JS. An unknown name throws; a `RequireAuthorization` that
    silently protects nothing is the failure worth ruling out.
  - `ReDocDocumentExtensions.RequireAuthorization(this ReDocDocument, params string[])` is the
    whole of per-document authorization and the reason that story is worth selling. Gating a page
    while `MapOpenApi()` serves the document it renders is a locked door beside an open window, and
    nothing here can reach that window: `MapOpenApi` and `MapSwagger` map **one** endpoint for
    every document, so there is no per-document thing to attach a policy to. Said on the document,
    one call does the three things that are only ever wanted together — policy on that document's
    own endpoints, the page serving its own spec so the policy covers it, and the document out of
    the other pages' pickers. Measured in `Sample.Own`: page and spec both 302 to `/sign-in` signed
    out, both 200 signed in, and the public pages' picker JSON doesn't name it.
    - It has to be said on the **document**, not on the returned builder, and that is the whole
      design: `ReDocSite.Build` freezes every page's markup and mounts the spec endpoints before
      `MapReDocDocuments` returns, so a convention applied afterwards can only reach the page.
      `ForDocument` therefore stays for everything else endpoint routing can do, and composes.
    - The gated record is `ReDocGatedDocument`, **internal**, deriving from core's now-unsealed
      `ReDocDocument`; the extension returns the base type. That is what keeps authorization out
      of the adapters by construction rather than by a runtime check — the type doesn't exist in
      a package they reference, so their consumers cannot write it and nothing has to throw.
      The cost: nothing anywhere may copy, rebuild or compare a `ReDocDocument`. A `with`
      expression or a reconstruction from parts would silently drop the gating. It was verified
      that nothing does before unsealing; keep it that way.
    - One gated document turns spec-serving on for **every** page in the call, not just itself.
      The public pages can't be left reading `MapOpenApi()`'s endpoint, since that one endpoint
      serves the gated document too — so the one-call form would otherwise produce a site whose
      public pages have no spec at all, which is the two-knob problem it exists to remove.
    - It throws rather than half-working in four places: a gated document with its own `SpecUrl`
      or alongside a non-default `SpecUrlTemplate` (a locked page in front of a spec we don't
      serve), a gated name no provider offers, and any gated document handed to the
      `UseReDocDocuments` middleware form, which carries no metadata to gate.
  - `ServeSpecs` is the same machinery without the gating, for a site that wants to serve its own
    specs for other reasons (dropping `MapOpenApi()`, one proxy rule, shape 2). With it on, each
    page serves its own document at `openapi.json` beside itself, inside that document's endpoint
    group. Generation goes through `OpenApiSpecSource`, the same
    `Microsoft.Extensions.ApiDescriptions.IDocumentProvider` hook `OpenApiDocumentNames` already
    resolves by name, so `MapOpenApi()` needn't be mapped at all and the output was diffed against
    that endpoint's: identical but for the assembly-name tag and the port. Three decisions worth
    keeping: the provider is resolved per request (an implementation may be scoped) while *which*
    provider is settled at startup; the spec is sent `no-store`, alone among the things this
    package serves, because a shared cache holding a gated document would hand it on; and
    `ServeSpecs` with a non-default `SpecUrlTemplate` throws rather than silently picking one of
    two answers about where a page reads its spec. `servers` in a generated document comes from
    the server's own addresses rather than the request, unlike `MapOpenApi`'s — an
    `AddDocumentTransformer` is the fix, and the README says so.
  - `assets/redoc.standalone.js.gz` is the ReDoc bundle, **checked in gzipped and served that way**.
    No Node, no `npm`, no build-time download: `assets/README.md` records the version, the npm
    tarball integrity and the upgrade steps. `gzip -n` on upgrade, or the file differs on every
    run. That note is **embedded into the test assembly**, and every row of its table is asserted
    against the shipped bundle — version, tarball name, the uncompressed sha256, and all three byte
    counts — so an upgrade that doesn't update the note fails a test rather than making the note a
    lie. The one row a test can't reach is the npm tarball's own integrity hash, for a file the
    repository doesn't contain.
  - `ReDocPage` (internal) builds the markup, the stylesheet, `redoc-init.js` and
    `redoc-colormode.js`. **Nothing is inline** — not the config, not the picker, not the
    color-mode bootstrap — which is the entire reason to own the template: `script-src 'self'`
    holds with no nonce. `style-src 'unsafe-inline'` is still unavoidable (styled-components
    injects rules at runtime, and setting `__webpack_nonce__` does not reach it on redoc 2.5.4 or
    3.0.1 — measured, both die with a blank page). Don't advertise nonce support.
  - The color mode is stamped on `<html>` server-side when it's known, and `redoc-colormode.js`
    does the rest from `localStorage['redoc.colorMode']` — ReDoc 3's own key. ReDoc does this with
    an *inline* script, which a strict CSP blocks, so without this the page renders light for a
    moment.
  - `ReDocSite` (internal) is everything worked out at startup: one `ReDocSitePage` per document
    holding its finished markup and files. Nothing is built per request, because nothing varies
    per request.
  - The bundle is served once for the whole site at `{RoutePrefix}/_redoc/`, and each page's
    `redoc-init.js` imports it by a relative href (`ReDocRelativePath`), so one cached copy serves
    every page and the URLs survive a path-rewriting proxy. `Cache-Control: immutable` for a year
    plus a strong ETag: Redoc.AspNetCore sends 1 MB uncompressed with no `Cache-Control` at all,
    Swashbuckle sends `max-age=0`.
  - A route pattern ignores its trailing slash, so `{route}/` and `{route}` are **one endpoint**,
    not two — mapping both throws `AmbiguousMatchException`. The handler checks the path itself and
    302s when the slash is missing, because serving the page there would leave every relative href
    resolving one folder up.
  - `ReDocPage.ConfigJson` writes the config with `Utf8JsonWriter`, never `JsonSerializer`: a slim
    or AOT host has reflection-based serialization off. `AdditionalConfigJson` is the escape hatch
    for ReDoc options this package doesn't surface, validated where it's set rather than on the
    page.
  - `ReDocDarkThemePresets` (internal) backs `ThemePreset` — Midnight / Nord / GitHubDark, each eleven
    `--color-warm-grey-N` values plus an accent, under `:root:root.dark`. That is a whole theme
    because of the ramp fact above, and it is **dark mode only**: all three are dark palettes.
    Setting `PrimaryColor` or `DarkModePrimaryColor` turns the preset's accent off rather than
    letting it lose — the preset's selector is one class heavier than the `:root:root` a
    `PrimaryColor` meant for both modes is written as, so ordering couldn't settle it. Each preset
    also carries the shade of its accent that reads on paper, which `AppendPrintCss` emits beside
    the restored light ramp. Adding a preset is a row in `PaletteOf` and an enum member.
  - `SearchablePicker` swaps the native `<select>` for a combobox: button, filter box and
    `role="listbox"`, filtering on a document's label **and** its group so a group name brings up
    everything under it (a `Links` entry has no group and filters on its label). It is a whole
    other control, so `ReDocDocumentPicker` holds a whole other template rather than placeholders
    in the `<select>` one — the useful property being that the frozen path is impossible to change
    from there. Off by default on purpose: a native `<select>` is better while the list fits on
    screen, and a phone renders it with its own picker. Two things that are not preferences.
    The popover is `position: fixed`, placed from the pill's rect in script, because ReDoc's
    sidebar is `overflow-y: auto` and would clip an absolutely positioned list — and the sidebar is
    where a site with enough documents to want search puts its picker; it also flips above the pill
    when there isn't room below, which is what `BottomRight` always is. And rows go in through
    `place` so grouping follows the `<optgroup>` rule exactly (heading where the group's first
    member falls, later members joining it), with the keyboard's list read back out of the DOM
    afterwards, in the order it ended up on screen.
    - Anything in here hidden with the `hidden` attribute — the popover, the list, the empty
      line — needs its own `[hidden] { display: none }` rule, because the attribute is backed by
      the browser's stylesheet and the `display` these carry on a class of ours outranks it. The
      failure is not subtle but reads as something else entirely: the popover is on screen from
      the moment it is built, covering its own pill, so the filter box is what you click, `open`
      never runs, and nothing it attaches — positioning, Escape, outside-click — exists.
      It shipped that way once, caught in the sample rather than by a test.
  - `PickerPlacement` and `ToolbarItemsPlacement` (`ReDocToolbarPlacement`: `TopRight`,
    `BottomRight`, `Sidebar`) place the picker and the links **independently**. The mechanism is
    one container per spot, found by that spot's own class: matching placements resolve to the
    same element at runtime and share a row, different ones each build their own. So placement
    hangs off `ReDocToolbar.ScriptFunctionFor`, i.e. off **creating the container** — each of the
    two scripts carries the variant for its own spot, whichever runs first creates, and the other
    appends to what it finds. `ReDocToolbarSpot` is core's internal spelling of the public enum,
    which lives in the host because only the host can place anything; its `Shared` member is the
    adapters' unplaced container and the reason their inlined script is still byte-identical.
  - Order inside a strip is CSS, never DOM order, because the two scripts append in load order and
    only one of them may exist. The picker carries `order: -1` in core (it leads the theme selector
    on the adapters, which must not move), so the host outweighs it by one class: a corner strip
    gets `.hlibz-toolbar .hlibz-doc-picker { order: 1 }` — links first, since the picker is the
    element that grows with a document's name and a row that ends with it doesn't shuffle — and
    `SidebarCss`, emitted last, puts `order: -1` back for a column. Both are emitted only when they
    can matter, so the stylesheet says what the page does.
  - For `Sidebar` the hook is `.menu-content`, **the one class ReDoc doesn't hash**; measured on
    3.0.1, a node inserted as its first child survives menu navigation, a colour-mode switch, the
    search dialog and scrolling. Two things the browser showed and nothing else would: ReDoc
    renders the sidebar after its bundle boots, so the strip starts over the page and moves once
    a `MutationObserver` sees the sidebar; and below ReDoc's own 50rem breakpoint the sidebar is
    `display: none` until its floating button opens it, so a `matchMedia` listener brings the
    strip back out there. Dropping either fallback leaves a phone, or a failed render, with no
    picker at all.
  - Under that same breakpoint core's base rule drops **every** strip into the page flow, which is
    also what keeps `BottomRight` off the floating menu button a phone puts in that corner. DOM
    order is then reading order, so the bottom-right strip inserts itself after every strip already
    at body level while the others insert first — the one case where creation order would otherwise
    decide what you see.
- `src/Hlibz.Redoc.Extensions/` — core: everything that doesn't touch a host's `ReDocOptions`.
  It keeps `FrameworkReference Microsoft.AspNetCore.App` but references no host package — ASP.NET
  is common ground to every host, so document validation, route computation and the prefix redirect
  live here instead of being duplicated per adapter. CI asserts the packed nuspec has no host
  dependency.
  - Core's workhorses stay `internal`; the adapters, the future host and the core test project
    reach them through `InternalsVisibleTo` entries in core's csproj, listed **by assembly name** —
    renaming any of those projects means editing that list. This is deliberately not a public
    composition API: the public surface is still only the options types a consumer writes, so
    nothing in here is a compatibility promise yet. The cost is that a third party can't write
    their own adapter. V2-PLAN's "core emits a model with two renderers" idea is the eventual
    replacement; don't build it before phase 3 needs it.
  - `ReDocDocumentsOptionsBase` — the neutral options (`RoutePrefix`,
    `RedirectToFirstDocument`, `SpecUrlTemplate`, `ValidateDocumentNames`, `Links`).
  - `ReDocDocumentsPlan` (internal) — everything `UseReDocDocuments` works out before it touches a
    host: `Validate`/`ValidateRoutes`/`RouteOf`, the document-name check, the prefix redirect, and
    per-page `SpecUrlOf`/`DocumentTitleOf`/`PickerFor`. Its exception messages and `paramName`s
    name the *public extension method's* parameters (`"documents"`, `"configure"`), not its own —
    they moved verbatim and tests assert on them. Two `Build` overloads: the `IApplicationBuilder`
    one registers the prefix redirect as middleware for the adapters, the `IServiceProvider` one
    returns it as a `ReDocPrefixRedirect` for a host that maps its own endpoints.
  - `ReDocDocumentPicker` exposes its `Css` and `BuildScript` separately from `Build`, because the
    adapters can only inline them through `HeadContent` while our host serves them as files. The
    adapters' output is byte-identical either way — it was diffed against the previous commit's
    served pages on both hosts, which is the standard any change in here should meet. The
    host-only additions — `<optgroup>` groups, `BuildScript`'s `spot`, which only chooses among
    `ReDocToolbar.ScriptFunctionFor`'s variants, and its `searchable`, which picks a different
    template entirely — resolve to the previous text at their defaults (`ReDocToolbarSpot.Shared`,
    no groups, no search), including the blank lines they'd occupy, precisely so that diff stays
    empty.
  - `ReDocDocument` is **not sealed**, so the host can carry per-document authorization on a record
    of its own. Nothing here may copy, rebuild or compare a document as a result — see the host's
    `RequireAuthorization` notes above.
  - `ReDocDocument.Unlisted` keeps a document out of the picker on *listed* pages; unlisted pages
    list everything. On the host, gating a document already implies it, so `Unlisted` is left for
    what it is actually for: a document you serve but would rather not advertise. The rule is about
    the page doing the asking, never its viewer, because each page is built once at startup and
    served to everyone as the same string —
    `ReDocDocumentsPlan.PickerPagesFrom` is where that is decided, and making it identity-aware
    would cost the whole "nothing is per-request" property `ReDocSite` is built on. It is a
    visibility flag and **not** access control; say so wherever it is documented, because a reader
    who assumes otherwise ships an open page.
  - `DarkThemeOptions` — customization surface: `PrimaryColor`/`PrimaryColorLight`/
    `PrimaryColorDark`/`AdditionalCss`, plus the font, `Favicon` and `PrintStyles` settings below.
    Everything else in the palette (success/warning/error/grays/etc.) is intentionally fixed —
    this is a themed package, not a general theming engine.
  - `ReDocDarkThemeDefaults` (internal) builds the actual `HeadContent` string and theme config
    `Dictionary<string, object>` consumed by `ReDocOptions`.
  - `DarkThemeOptions.EnableThemeSelector(ReDocTheme)` opts into a Light/System/Dark selector
    (System follows `prefers-color-scheme` live).
    `ReDocThemeSelector` (internal) emits its CSS + a `<script>` via `HeadContent`, so no
    `IndexStream` override is needed. The script intercepts the `window.Redoc` global as the bundle
    assigns it and returns a wrapped copy, because webpack exports are getter-only and can't be
    reassigned in place. The wrapped `init` drops the dark `theme` config for Light. A switch runs
    `Redoc.destroy` + re-init, because ReDoc builds its theme once per store. With the selector on,
    the dark CSS is scoped under `html[data-redoc-theme="dark"]`. With it off, the scope is plain
    `html`.
  - `ReDocFonts` (internal) backs `UseSystemFonts()` / `FontFamily` / `HeadingsFontFamily` /
    `CodeFontFamily` / `LoadGoogleFonts`. Three things to know. (1) The families go in ReDoc's
    `typography` theme config, but the selector drops that config for Light, so the script also
    carries a fonts-only theme (`lightThemeConfig`) — otherwise Light reverts to Montserrat/Roboto
    with nothing loading them. (2) Setting the families doesn't stop the request: the `<link>` sits
    above `%(HeadContent)` in the index template, so `UseDarkTheme` wraps `IndexStream` and strips
    the tag instead — which is why a caller's own `IndexStream` must be set first. (3) The page
    carries the config as `JSON.parse('%(ConfigObject)')`, a JS string literal that eats
    backslashes, so any `\uXXXX` the serializer emits breaks `JSON.parse` on the *whole* config
    (blank page, one console error). Font stacks are therefore stripped of quotes — optional in
    CSS — and throw on anything else needing escaping. Any future free-text theme setting has the
    same constraint.
  - `ReDocDarkThemeDefaults.BuildHeadContent` also emits the `Favicon` link and, unless
    `PrintStyles` is off, an `@media print` block. The palette can't be re-pointed per media query
    (ReDoc builds it once in JS), so printing overrides the rendered colors wholesale and hides the
    sidebar and toolbar. Collapsed sections stay collapsed: ReDoc doesn't render their content
    until opened, so CSS can't reach it. The block goes before `AdditionalCss`, so a caller's print
    rules win at equal specificity.
  - `ColorUtils` (internal) — simple HSL lighten/darken, used only when a caller supplies a
    custom `PrimaryColor` without also specifying explicit light/dark shades. The zero-config
    default path uses fixed, hand-picked values instead of the computed fallback.
  - `UseReDocDocuments` mounts one `UseReDoc` page per `ReDocDocument`, configured by
    `ReDocDocumentsOptions`. `UseReDoc` runs its setup action immediately, so all routes are
    computed up front (`ReDocDocumentPage`, via `ReDocDocumentsPlan`), and `ConfigureReDoc` may not
    change `RoutePrefix` (it throws).
    Redoc.AspNetCore always serves a page at `{PathBase}/{route}/index.html`, so
    `ReDocDocumentPicker` (internal) builds each page's hrefs relative to its own route depth
    (`../` per segment). The hrefs need no PathBase and survive path-rewriting proxies. `~/` link URLs
    resolve the same way. The picker JSON is embedded in a `<script>` and relies on
    `ReDocPickerJson`, a source-generated `JsonSerializerContext` (the reflection-based
    `JsonSerializer.Serialize` overloads aren't trim/AOT-clean), whose default escaping makes the
    JSON safe inside `<script>`. `DocumentTitle` is HTML-encoded (`ReDocDocumentsPlan`
    `.DocumentTitleOf`) because Redoc.AspNetCore writes it into `<title>` raw — as does every host
    so far, which is why the encoding sits in core rather than the adapter.
  - `OpenApiSpecSource` (internal) generates one document's JSON through that same provider's
    `GenerateAsync(string, TextWriter)`, for the host's `ServeSpecs`. Resolution is by exact
    `(string, TextWriter)` signature, not by name — .NET 10's provider carries more than one
    `GenerateAsync`. The provider's *own* spelling of the name is what gets passed back to it
    (matching is case-insensitive, Swashbuckle's lookup is not), and the document is buffered
    rather than streamed so a generator that throws half-way produces a 500 rather than a 200
    carrying half a document.
  - `OpenApiDocumentNames` (internal) backs `ReDocDocumentsOptionsBase.ValidateDocumentNames` (on by
    default): a configured name that isn't a registered OpenAPI document throws at startup instead
    of serving a page whose spec 404s. It resolves the *internal*
    `Microsoft.Extensions.ApiDescriptions.IDocumentProvider` by type name across loaded assemblies
    and unions every `GetDocumentNames()`, which covers both `Microsoft.AspNetCore.OpenApi` and
    Swashbuckle (and NSwag) in one lookup. The interface is internal by design — `dotnet
    getdocument` resolves it by name too — so reflection is the only option even on net9/net10, and
    multi-targeting wouldn't help. Returning `null` rather than an empty set when no provider
    resolves is what keeps "can't verify" from throwing; every reflection step is wrapped per
    assembly for the same reason. Matching is case-insensitive because `MapOpenApi` lowercases the
    requested name before its lookup. Documents with an explicit `SpecUrl`, and all documents when
    `SpecUrlTemplate` has no `{documentName}`, are skipped: only names that actually build a spec
    URL are checked.
  - `ReDocToolbar` (internal) is the top-right container both the theme selector and the picker
    append to, created on first use as the first child of `<body>`. Below 50rem (ReDoc's small
    breakpoint), it drops out of `position: fixed` into the page flow so it doesn't cover the title.
    It also declares the `--hlibz-control-*` color tokens (bg, border, fg, fg-hover, accent,
    accent-fg) with light values under `:where(html)` (zero specificity).
    `ReDocDarkThemeDefaults.BuildHeadContent` overrides them under the dark root selector, so both
    controls follow the active theme without knowing which features are on. It also owns where a
    container *goes*: `ScriptFunctionFor(spot)` returns one creator per `ReDocToolbarSpot`, each
    identifying its container by that spot's class — which is what lets one page carry several
    strips and each script find only its own.
- `samples/Sample.Shared/` — the four OpenAPI documents (the fourth, `internal`, is what the samples mark `Unlisted` and `Sample.Own` gates) and the endpoints behind them,
  `AddSampleDocuments()` / `MapSampleEndpoints()`, so every sample renders the *same* spec and the
  samples can be run together and compared. Each document is registered with `AddOpenApi(name)` and
  each endpoint tagged with `.WithGroupName(name)`, because untagged endpoints land in every
  document. The models are deliberately shaped to exercise the CSS overrides in
  `ReDocDarkThemeDefaults`, not to model a realistic domain: `Widget` nests 5 levels deep
  (`Manufacturer` → `Facility` → `GeoCoordinates` → `CoordinatePrecision` → `GeodeticDatum`) to
  surface ReDoc's alternating nested-schema background bug, every `widgets` endpoint documents a
  `500` response alongside its own negative response (`400`/`404`) with a distinct model to check
  multiple response-schema/status-tab combinations render correctly, and every property carries a
  `[Description]` so field descriptions are visible everywhere. It's a plain `Microsoft.NET.Sdk`
  library, so the minimal-API extension methods the web-SDK samples get implicitly are imported by
  hand.
- `samples/Sample.Own/` (:5499) — the same shared documents on our own host. Its `Program.cs` sets
  the verified Content-Security-Policy itself, so the sample proves the policy rather than the
  README asserting it, and it gates `internal` with a cookie scheme and `/sign-in` so
  `new ReDocDocument(...).RequireAuthorization("Employees")` is a running flow. It sets no
  `ServeSpecs` (the gating implies it) and maps **no `MapOpenApi()`**: that would reopen
  `/openapi/internal.json` to anyone, which is the hole the sample exists to show closed — so
  don't add it back when the sample needs a spec URL for something. It can't be line-for-line identical to the other two: there is no
  `UseDarkTheme` here and the mapping call is `MapReDocDocuments`.
- `samples/Sample.RedocAspNetCore/` (:5299) and `samples/Sample.Swashbuckle/` (:5399) — the same
  app on each host, used as runnable demos and to produce the README screenshot
  (`/api-docs/widgets/`, dark via System, from the 5299 one). Their `Program.cs` files are
  line-for-line identical apart from the two `using`-level package differences: the adapters differ,
  the code a consumer writes doesn't. Both show a `~/` picker link and configure `UseSystemFonts()`
  on the Facilities page only, so the font difference is visible by switching documents. Keeping
  them identical is what makes a host difference show up as a visible difference.
- `tests/Hlibz.Redoc.Extensions.Tests/` — the host-free suite, on the library's own `net8.0`, with
  no host reference. It asserts on what `ReDocDarkThemeDefaults.BuildHeadContent`/`BuildConfigValue`
  produce, reached directly through `InternalsVisibleTo`, so those assertions hold for every host
  rather than only the one that happens to be wired up. Anything that is really a statement about
  the theme, the selector, the fonts or the picker belongs here, not in an adapter suite.
- `tests/Conformance/` — **one** set of assertions, compiled once per adapter: what `UseDarkTheme`
  writes to `ConfigObject.AdditionalItems` and `HeadContent`, the `IndexStream` rewrite against
  that host's *actual* stock `index.html` (the font `<link>` present then stripped while
  `%(HeadContent)` and `redoc.standalone.js` survive), the `UseReDocDocuments` pipeline tests built
  on `WebApplication.CreateSlimBuilder()` and requesting `/{route}/index.html`, and the
  document-name check. Not a project: each adapter test project links it with
  `<Compile Include="..\Conformance\*.cs" .../>`.
  - It needs no abstraction layer, because both adapters use identical type names in
    `Hlibz.Redoc.Extensions`. The single exception is the host's own options type, supplied by a
    one-line `global using ReDocOptions = ...;` alias in each project's `ConformanceHost.cs`.
  - **A conformance test that needs a per-host tweak is a finding, not a nuisance**: either a real
    behaviour difference that belongs in `ConformanceHost`, or a bug in the adapter. Don't fork the
    assertion. This is the permanent replacement for phase 1's throwaway "capture every string
    before and after and diff it" harness — the head content is one large string, and a lost
    newline passes every `Contains` assertion.
- `tests/Hlibz.Redoc.AspNetCore.Tests/` — our host's own suite. `ReDocAuthorizationShapesTests`
  holds the README's three setup examples (all public / all internal / mixed) as the status codes
  an anonymous caller gets, including the one that still leaks — a gated document with
  `MapOpenApi` left open — so a reader choosing a shape is choosing on something that can't drift.
  Edit the README's examples and that file together. `ReDocGatedDocumentTests` is the mechanism
  under those shapes: which endpoints carry the policy, the site-wide spec implication, and the
  four ways gating refuses rather than half-working. It does **not** share
  `tests/Conformance`: those assertions are all `ReDocOptions`, `ConfigObject.AdditionalItems`,
  `HeadContent` and `IndexStream`, four concepts this host doesn't have. It starts a real Kestrel
  on port 0 (`ReDocTestApp`) because **endpoint routing matches nothing in a hand-built
  pipeline** — `((IApplicationBuilder)app).Build()` off a `WebApplication` 404s every mapped
  route, which is why the adapters' middleware-based harness couldn't be reused.
- `tests/Hlibz.Redoc.Extensions.RedocAspNetCore.Tests/` and
  `tests/Hlibz.Redoc.Extensions.Swashbuckle.Tests/` — the conformance source plus that one alias
  file, nothing else. Both override `TargetFramework` to `net10.0` and reference
  `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore.SwaggerGen` (test-only) so the
  document-name check runs against the real providers — the type and method names it resolves by
  string are the fragile part, and a stand-in wouldn't protect them. The library under test is
  still its `net8.0` assembly. Swashbuckle must be ≥ 10.x: 9.x pins Microsoft.OpenApi 1.x, which
  collides with what `Microsoft.AspNetCore.OpenApi` 10.x brings in.
- What the suites can't reach is the rendered page, so a change to the theme CSS still wants both
  samples open side by side. Phase 2 checked that way that redoc 2.0.0 and 2.5.4 render the theme
  **identically**: a probe of the computed colors (body, scrollbars, sidebar, `h5` captions, every
  selected tab, the `--hlibz-control-*` tokens, the nested-schema wrappers) returned byte-identical
  output on both hosts. Both hardcoded-color bugs the overrides exist for are still present in
  2.5.4, so none of those overrides is inert.

## Releasing

`.github/workflows/release.yml` runs on a pushed `vX.Y.Z` tag: it builds/tests, packs every project
under `src/` with `-p:Version` from the tag, and publishes via NuGet Trusted Publishing (OIDC, no
API key; see https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). The job uses the
`release` GitHub Environment (must match the Environment on the nuget.org trusted-publishing
policy) and a `${{ secrets.NUGET_USER }}` repo secret holding the nuget.org profile username (not
the email).

To publish: don't bump anything (the tag drives the version — `Version` in
`Directory.Build.props` is only the local/branch default), just
`git tag vX.Y.Z && git push origin vX.Y.Z`.

**Every package ID needs its own trusted-publishing policy on nuget.org, created before the first
tag that would publish it** — a new ID without one fails the push. Adding a project under `src/`
therefore means adding it to the release workflow's pack list *and* creating that policy. Policies
reference `owner/repo`, so a repo rename means updating all of them.

All four packages ship **one version**, and that is forced rather than chosen: a `ProjectReference`
packs as `<dependency id="Hlibz.Redoc.Extensions" version="X.Y.Z" />`, which NuGet reads as
`>= X.Y.Z`, so an adapter published without a core of the same version fails a consumer's restore.
Releasing one package alone would mean per-package versions, a tag prefix per package and a looser
dependency range — don't start down that path for a one-package fix. The lockstep cost is that a
package with no changes still gets a new number, which is what the release notes are for.

### When asked to make a release

A pushed tag publishes to nuget.org, and nuget.org never lets a version be republished — so the
tag push is the point of no return and needs the user's explicit go-ahead. The steps:

1. **Agree the version.** Read `git log <last tag>..HEAD` and say which part of semver the diff
   calls for, including which of the four packages actually changed. Don't pick it silently.
2. **Draft the notes before tagging**, from that commit range, in this shape: a line per change
   grouped under the package it belongs to, then a `Published for lockstep` line naming the
   packages that are unchanged and only got a new number. That line is the whole reason these notes
   exist — without it, a bumped version with no changes looks like a mystery on nuget.org.
3. **Tag and push** only on the user's word: `git tag vX.Y.Z && git push origin vX.Y.Z`.
4. **Create the GitHub Release** on that tag with the drafted notes —
   `gh release create vX.Y.Z --title vX.Y.Z --notes-file <file>`. `gh` is authenticated here with
   `repo` scope, so do it rather than asking. If it isn't (another machine, expired token, a
   sandbox that blocks it), print the notes verbatim and ask the user to create the release
   themselves — never leave a tag with no notes.
5. **Check the run**, with `gh run list --workflow release.yml`, and say whether all four packages
   were pushed. `--skip-duplicate` means a re-run of the same tag is safe but silent, so a green
   job doesn't by itself prove a package is new.

The GitHub Release is the only home for release notes: no `CHANGELOG.md` and no
`PackageReleaseNotes`, so there is nothing to keep in sync. Releases are reachable from each
package's nuget.org page through its repository link.

## Conventions

- File-scoped namespaces, explicit types (no `var`), 4-space indent, LF, UTF-8 (no BOM), 100-char
  lines — see `.editorconfig`.
- `Directory.Packages.props` manages package versions centrally; csproj files reference packages
  without a `Version=` attribute.
- `Version` lives in `Directory.Build.props`, not in any csproj: core, the adapters and the host
  ship in lockstep, so one `-p:Version` from the release tag has to drive all of them.
- No StyleCop/SonarAnalyzer: this is a small single-purpose package, kept intentionally
  lightweight. `TreatWarningsAsErrors` is still on.
- The library sets `IsAotCompatible`, so the trim/AOT analyzers run with warnings as errors: new
  reflection or reflection-based JSON has to be source-generated or suppressed with a justification
  that holds. It's a claim about this assembly only — Redoc.AspNetCore serializes the config
  reflectively, so a real NativeAOT host still won't run it.
- The package icon's source is `docs/icon.svg`. `docs/icon.png` (128x128) is packed as
  `PackageIcon` and shown at the top of README through its `raw.githubusercontent.com` URL on
  `main`, because nuget.org renders neither relative image paths nor raw HTML. So a changed
  icon shows in README only once it's pushed.

## Documentation layout

**Each package packs its own `src/<package>/README.md`**, not the root one. nuget.org shows one
package's page at a time — a shared README made a Swashbuckle consumer read 400 lines about the
other host first — and in-page anchors (`#three-shapes`) don't resolve there, so the per-package
READMEs are self-contained and link out with absolute GitHub URLs instead. CI asserts a `README.md`
at each package root and a matching `<readme>` element in the nuspec, so dropping the `None` item
fails the build rather than publishing a package with no page.

- `README.md` (root) is the GitHub face only: it picks a package, compares the three, and links to
  the per-package READMEs. It documents none of them in full.
- `src/Hlibz.Redoc.Extensions/README.md` is a stub — that package does nothing on its own.
- The two adapter READMEs are near-identical by design, for the same reason the adapters' code is:
  the host-specific lines are `SpecUrl` (method vs property), the embedded ReDoc version, and the
  nonce/`__webpack_nonce__` result. Keep them in sync.
- `docs/DESIGN.md` holds rationale — why a decision was made and what was measured. Anything in a
  README that explains a decision rather than telling a reader what to do belongs there; that split
  is what keeps the READMEs usable.
- `CONTRIBUTING.md` holds the build/test/sample/release instructions. The commands in this file and
  in there are the same set; edit both.
- `Hlibz.Redoc.AspNetCore`'s README carries the three authorization shapes, asserted in
  `ReDocAuthorizationShapesTests` — edit them together.
