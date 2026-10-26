# Contributing

Bug reports, ideas and pull requests are welcome —
[open an issue](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/issues) first
if the change is large, so we can agree on the shape before you write it.

## Requirements

.NET 10 SDK. The libraries target `net8.0`; the samples target `net10.0` so they can use the native
`AddOpenApi()` / `MapOpenApi()` APIs.

## Build and test

```bash
dotnet build
```

Four test suites, each run on its own — test projects use Microsoft Testing Platform, so
`dotnet test` needs `--project` rather than a bare directory:

```bash
dotnet test --project tests/Hlibz.Redoc.Extensions.Core.Tests                     # the host-free engine
dotnet test --project tests/Hlibz.Redoc.Extensions.RedocAspNetCore.Tests     # conformance, host 1
dotnet test --project tests/Hlibz.Redoc.Extensions.Swashbuckle.Tests         # conformance, host 2
dotnet test --project tests/Hlibz.Redoc.AspNetCore.Tests                     # the ReDoc 3 host
```

`tests/Conformance/` is **one** set of assertions, compiled once per adapter rather than being a
project of its own. A conformance test that needs a per-host tweak is a finding, not a nuisance:
either a real behaviour difference that belongs in `ConformanceHost`, or a bug in the adapter.
Don't fork the assertion.

## Run the samples

Three samples serve the same four OpenAPI documents, one per package:

```bash
dotnet run --project samples/Sample.Own               # :5499 — ReDoc 3, our own host
dotnet run --project samples/Sample.RedocAspNetCore   # :5299 — dark theme, ReDoc 2.0.0
dotnet run --project samples/Sample.Swashbuckle       # :5399 — dark theme, ReDoc 2.5.4
```

Each opens `/api-docs` and redirects to the first document. Run them together to compare: the two
adapter samples have line-for-line identical `Program.cs` files, so anything that differs between
them is a host difference.

`Sample.Own` sets the strict CSP from its README itself, so a running app proves the policy rather
than the docs asserting it, and gates its `internal` document with
`.RequireAuthorization("Employees")` on the document, behind a cookie sign-in (the "Sign in" toolbar
button). It maps no `MapOpenApi()` at all — `curl` the internal page and its `openapi.json` signed
out and both redirect to `/sign-in`, and the public pages' pickers don't name it. The adapter
samples list the same document as `Unlisted`, which hides it and protects nothing.

**The test suites can't reach the rendered page.** A change to the theme CSS, the picker or the
toolbar wants both adapter samples open side by side, and a change to the ReDoc 3 host wants
`Sample.Own` in a browser.

## Conventions

- File-scoped namespaces, explicit types (no `var`), 4-space indent, LF, UTF-8 without BOM,
  100-character lines — see `.editorconfig`.
- `Directory.Packages.props` manages package versions centrally; csproj files reference packages
  without a `Version=` attribute.
- `Version` lives in `Directory.Build.props`, not in any csproj: all four packages ship in lockstep.
- No StyleCop or SonarAnalyzer — this is a small, single-purpose set of packages. But
  `TreatWarningsAsErrors` is on, and the libraries set `IsAotCompatible`, so new reflection or
  reflection-based JSON has to be source-generated or suppressed with a justification that holds.

## Documentation

- Each published package has its **own** `README.md`, packed into that package, because nuget.org
  shows one package at a time and in-page anchors don't resolve there. Keep them self-contained and
  avoid in-page links.
- The root `README.md` chooses a package and compares them; it doesn't document any of them in full.
- Rationale — why a decision was made, what was measured — goes in
  [`docs/DESIGN.md`](docs/DESIGN.md), not into a README.
- The authorization examples in `Hlibz.Redoc.AspNetCore`'s README are asserted in
  `ReDocAuthorizationShapesTests`. Edit them together.
- `assets/README.md` in `src/Hlibz.Redoc.AspNetCore` records the ReDoc bundle's version and hashes,
  and every row of its table is asserted against the shipped bundle — so upgrading the bundle means
  updating that note, or a test fails.

## Releasing

`.github/workflows/release.yml` runs on a pushed `vX.Y.Z` tag: it builds, tests, packs every project
under `src/` with `-p:Version` from the tag, and publishes via NuGet Trusted Publishing. Don't bump
anything by hand — the tag drives the version:

```bash
git tag vX.Y.Z && git push origin vX.Y.Z
```

Every package ID needs its own trusted-publishing policy on nuget.org, created before the first tag
that would publish it. Adding a project under `src/` therefore means adding it to the release
workflow's pack list *and* creating that policy.

All four packages ship one version, because a `ProjectReference` packs as a `>= same version`
dependency on the engine — an adapter published without a core of that version fails a consumer's
restore. So a fix in one package still releases all four, and a package with no changes gets a new
number anyway.

Which is why **every tag gets a GitHub Release** whose notes group the changes by package and then
name the packages that are unchanged and only bumped for lockstep. The release is the only home for
those notes: there is no `CHANGELOG.md` and no `PackageReleaseNotes` to keep in sync.

```bash
gh release create vX.Y.Z --title vX.Y.Z --notes-file notes.md
```
