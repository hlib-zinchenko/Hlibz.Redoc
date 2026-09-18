# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A small NuGet package of add-ons for [Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore)'s
ReDoc UI middleware: a dark theme (a CSS string plus a ReDoc theme config dictionary), an optional
Light/System/Dark theme selector, and a document picker that serves several OpenAPI documents as
ReDoc pages with a dropdown between them. Renamed from `Hlibz.Redoc.AspNetCore.DarkTheme`;
`UseDarkTheme()` must keep behaving as it did there.

## Requires

* .NET 10 SDK (the library itself targets `net8.0`; the sample targets `net10.0` to use the native `AddOpenApi()`/`MapOpenApi()` APIs, which require net9.0+)

## Commands

```bash
# Build everything
dotnet build

# Run all tests
dotnet test --project tests/Hlibz.Redoc.AspNetCore.Extensions.Tests

# Run the sample app (opens http://localhost:5299/api-docs automatically, see launchSettings.json)
dotnet run --project samples/Hlibz.Redoc.AspNetCore.Extensions.Sample

# Pack the library locally
dotnet pack src/Hlibz.Redoc.AspNetCore.Extensions -c Release -o ./nupkg
```

Test projects use Microsoft Testing Platform (`test.runner` in `global.json`), so `dotnet test`
needs `--project <path>` rather than a bare directory argument.

## Architecture

- `src/Hlibz.Redoc.AspNetCore.Extensions/` — the library. Two public entry points:
  `RedocOptionsExtensions.UseDarkTheme(this ReDocOptions, Action<DarkThemeOptions>?)` and
  `ReDocDocumentsBuilderExtensions.UseReDocDocuments(this IApplicationBuilder, ...)`.
  - `DarkThemeOptions` — customization surface: `PrimaryColor`/`PrimaryColorLight`/
    `PrimaryColorDark`/`AdditionalCss`. Everything else in the palette (success/warning/error/
    grays/etc.) is intentionally fixed — this is a themed package, not a general theming engine.
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
  - `ColorUtils` (internal) — simple HSL lighten/darken, used only when a caller supplies a
    custom `PrimaryColor` without also specifying explicit light/dark shades. The zero-config
    default path uses fixed, hand-picked values instead of the computed fallback.
  - `UseReDocDocuments` mounts one `UseReDoc` page per `ReDocDocument`, configured by
    `ReDocDocumentsOptions` (`RoutePrefix`, `RedirectToFirstDocument`, `SpecUrlTemplate`, `Links`,
    `ConfigureReDoc`). `UseReDoc` runs its setup action immediately, so all routes are computed up
    front (`ReDocDocumentPage`), and `ConfigureReDoc` may not change `RoutePrefix` (it throws).
    Redoc.AspNetCore always serves a page at `{PathBase}/{route}/index.html`, so
    `ReDocDocumentPicker` (internal) builds each page's hrefs relative to its own route depth
    (`../` per segment). The hrefs need no PathBase and survive path-rewriting proxies. `~/` link URLs
    resolve the same way. The picker JSON is embedded in a `<script>` and relies on
    `JsonSerializer`'s default escaping. `DocumentTitle` is HTML-encoded because Redoc.AspNetCore
    writes it into `<title>` raw.
  - `ReDocToolbar` (internal) is the top-right container both the theme selector and the picker
    append to, created on first use as the first child of `<body>`. Below 50rem (ReDoc's small
    breakpoint), it drops out of `position: fixed` into the page flow so it doesn't cover the title.
    It also declares the `--hlibz-control-*` color tokens (bg, border, fg, fg-hover, accent,
    accent-fg) with light values under `:where(html)` (zero specificity).
    `ReDocDarkThemeDefaults.BuildHeadContent` overrides them under the dark root selector, so both
    controls follow the active theme without knowing which features are on.
- `samples/Hlibz.Redoc.AspNetCore.Extensions.Sample/` — a minimal API used both as a runnable demo
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
- `tests/Hlibz.Redoc.AspNetCore.Extensions.Tests/` — smoke tests on the built config
  dictionary/head content shape. The `UseReDocDocuments` tests build a real pipeline with
  `WebApplication.CreateSlimBuilder()` and request `/{route}/index.html`, so they check the picker
  through the public API without `InternalsVisibleTo`.

## Releasing

`.github/workflows/release.yml` runs on a pushed `vX.Y.Z` tag: it builds/tests, packs
`src/Hlibz.Redoc.AspNetCore.Extensions` with `-p:Version` from the tag, and publishes via NuGet
Trusted Publishing (OIDC, no API key; see
https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). The job uses the `release` GitHub
Environment (must match the Environment on the nuget.org trusted-publishing policy) and a
`${{ secrets.NUGET_USER }}` repo secret holding the nuget.org profile username (not the email).

To publish: don't bump anything in the csproj (the tag drives the version), just
`git tag vX.Y.Z && git push origin vX.Y.Z`.

## Conventions

- File-scoped namespaces, explicit types (no `var`), 4-space indent, LF, UTF-8 (no BOM), 100-char
  lines — see `.editorconfig`.
- `Directory.Packages.props` manages package versions centrally; csproj files reference packages
  without a `Version=` attribute.
- No StyleCop/SonarAnalyzer: this is a small single-purpose package, kept intentionally
  lightweight. `TreatWarningsAsErrors` is still on.
