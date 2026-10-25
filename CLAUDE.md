# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Add-ons for ReDoc UI middleware: a dark theme (a CSS string plus a ReDoc theme config dictionary),
an optional Light/System/Dark theme selector, and a document picker that serves several OpenAPI
documents as ReDoc pages with a dropdown between them.

As of v2 this is **two packages**, not one. `Hlibz.Redoc.Extensions` is the host-agnostic engine;
`Hlibz.Redoc.Extensions.RedocAspNetCore` is the adapter that wires it to
[Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore)'s `ReDocOptions`. The split
exists because `Redoc.AspNetCore` is effectively abandoned while
`Swashbuckle.AspNetCore.ReDoc` has ~300× the downloads, and a package that extends one host's
`ReDocOptions` type can never serve the other. Adapters have to be separate packages: NuGet has no
optional dependencies, so one package referencing both hosts would make every consumer download two
embedded ReDoc bundles.

Lineage, and the compatibility it obliges: `Hlibz.Redoc.AspNetCore.DarkTheme` →
`Hlibz.Redoc.AspNetCore.Extensions` → `Hlibz.Redoc.Extensions.RedocAspNetCore`. `UseDarkTheme()`
must keep behaving as it did in all of them. Namespace is `Hlibz.Redoc.Extensions` for everything,
core and adapters alike, so a consumer writes one `using` whichever adapter they installed — the
assembly name and the namespace deliberately differ for the adapters.

## Requires

* .NET 10 SDK (the library itself targets `net8.0`; the sample targets `net10.0` to use the native `AddOpenApi()`/`MapOpenApi()` APIs, which require net9.0+)

## Commands

```bash
# Build everything
dotnet build

# Run the tests — two suites, one per project under test
dotnet test --project tests/Hlibz.Redoc.Extensions.Tests
dotnet test --project tests/Hlibz.Redoc.Extensions.RedocAspNetCore.Tests

# Run the sample app (opens http://localhost:5299/api-docs automatically, see launchSettings.json)
dotnet run --project samples/Sample.RedocAspNetCore

# Pack both packages locally
dotnet pack src/Hlibz.Redoc.Extensions -c Release -o ./nupkg
dotnet pack src/Hlibz.Redoc.Extensions.RedocAspNetCore -c Release -o ./nupkg
```

Test projects use Microsoft Testing Platform (`test.runner` in `global.json`), so `dotnet test`
needs `--project <path>` rather than a bare directory argument — which is also why there is no
one-command "run all tests": each suite gets its own invocation, in CI too.

## Architecture

- `src/Hlibz.Redoc.Extensions.RedocAspNetCore/` — the adapter, and the whole host-specific
  surface: three files holding the two public entry points,
  `RedocOptionsExtensions.UseDarkTheme(this ReDocOptions, Action<DarkThemeOptions>?)` and
  `ReDocDocumentsBuilderExtensions.UseReDocDocuments(this IApplicationBuilder, ...)`, plus
  `ReDocDocumentsOptions : ReDocDocumentsOptionsBase` adding the one genuinely host-typed member,
  `ConfigureReDoc` (`Action<ReDocOptions, ReDocDocument>`). Deriving the options rather than
  generalizing them keeps the public type name a consumer writes as `ReDocDocumentsOptions` on
  every host. `UseReDocDocuments` is ~25 lines: build options, call `ReDocDocumentsPlan.Build`,
  loop its pages calling `UseReDoc`. The `ArgumentNullException.ThrowIfNull` pair stays here rather
  than moving into `Build`, so the null checks still run before a caller's `configure` does.
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
    they moved verbatim and tests assert on them.
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
    controls follow the active theme without knowing which features are on.
- `samples/Sample.RedocAspNetCore/` — a minimal API used both as a runnable demo
  and to produce the README screenshot (`/api-docs/widgets/`, dark via System). It serves 3
  documents through `UseReDocDocuments`: `widgets`, `manufacturers` and `facilities`. Each is
  registered with `AddOpenApi(name)`, and each endpoint is tagged with `.WithGroupName(name)`,
  because untagged endpoints land in every document. It also shows a `~/` picker link. Its models
  are deliberately shaped to exercise the CSS overrides in `ReDocDarkThemeDefaults`, not to model
  a realistic domain: `Widget` nests 5 levels deep (`Manufacturer` → `Facility` →
  `GeoCoordinates` → `CoordinatePrecision` → `GeodeticDatum`) to surface ReDoc's alternating
  nested-schema background bug, every `widgets` endpoint documents a `500` response alongside its
  own negative response (`400`/`404`) with a distinct model to check
  multiple response-schema/status-tab combinations render correctly, and every property carries a
  `[Description]` so field descriptions are visible everywhere.
- `tests/Hlibz.Redoc.Extensions.Tests/` — the host-free suite, on the library's own `net8.0`, with
  no host reference. It asserts on what `ReDocDarkThemeDefaults.BuildHeadContent`/`BuildConfigValue`
  produce, reached directly through `InternalsVisibleTo`, so those assertions hold for every host
  rather than only the one that happens to be wired up. Anything that is really a statement about
  the theme, the selector, the fonts or the picker belongs here, not in an adapter suite.
- `tests/Hlibz.Redoc.Extensions.RedocAspNetCore.Tests/` — what genuinely needs this host:
  that `UseDarkTheme` writes to `ConfigObject.AdditionalItems` and `HeadContent` at all, the
  `IndexStream` rewrite against Redoc.AspNetCore's *actual* stock `index.html` (the font `<link>`
  being present then stripped while `%(HeadContent)` and `redoc.standalone.js` survive), and the
  `UseReDocDocuments` pipeline tests, which build a real pipeline with
  `WebApplication.CreateSlimBuilder()` and request `/{route}/index.html` — so the picker is checked
  through the public API. This project overrides `TargetFramework` to `net10.0` and references
  `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore.SwaggerGen` (test-only) so the
  document-name check runs against the real providers — the type and method names it resolves by
  string are the fragile part, and a stand-in wouldn't protect them. The library under test is
  still its `net8.0` assembly. Swashbuckle must be ≥ 10.x: 9.x pins Microsoft.OpenApi 1.x, which
  collides with what `Microsoft.AspNetCore.OpenApi` 10.x brings in.
- Refactors that move code between core and an adapter are checked by capturing every string the
  library emits before and after and diffing them, not just by the test suites: the head content is
  one large string and a lost newline passes every `Contains` assertion. V2-PLAN's phase 2
  conformance suite is the permanent version of that idea.

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
