![Hlibz.Redoc.AspNetCore.Extensions icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/main/docs/icon.png)

# Hlibz.Redoc.Extensions

Add-ons for ReDoc UI middleware, on either host —
[Swashbuckle.AspNetCore.ReDoc](https://www.nuget.org/packages/Swashbuckle.AspNetCore.ReDoc) or
[Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore):

- **Dark theme.** One line fixes the colors ReDoc hardcodes outside its own theme config
  (scrollbar, selection, section captions, nested-schema backgrounds, sample-tab text) and adds a
  full dark color palette.
- **Light/System/Dark selector.** Viewers pick a theme, or follow their OS setting.
- **Document picker.** Several OpenAPI documents, each on its own ReDoc page, with a dropdown to
  switch between them, like Swagger UI's definition selector.

[![CI](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Hlibz.Redoc.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.Extensions)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hlibz.Redoc.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.Extensions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/LICENSE)

![Dark ReDoc UI screenshot](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/main/docs/screenshot.png)

## Why

When I started using ReDoc, the first thing I wanted was a dark theme. The original
[Redoc.AspNetCore](https://github.com/jonashendrickx/Redoc.AspNetCore) package doesn't ship one,
and I couldn't find another NuGet package that did.

So I asked Claude Code to build one for my private project. That worked because the authors of
Redoc.AspNetCore made its API very configurable: the theme config and `HeadContent` hooks were
all the AI needed to produce a proper dark ReDoc page.

Then I figured I'm probably not the only one who wants dark API docs. So I moved the theme into
this public repo and NuGet package for the community to use.

Next I needed several OpenAPI documents in one app. Swagger UI has a dropdown for that, but ReDoc
shows one spec per page. So the package got a document picker, and a broader name to go with it:
it used to be `Hlibz.Redoc.AspNetCore.DarkTheme`.

## What it fixes

[ReDoc](https://github.com/Redocly/redoc)'s own `theme` config object covers most of its UI.
A few things are hardcoded to light-theme colors outside it, though, and stay light no matter what
the `theme` config says:

- The scrollbar, text selection, and the small section captions above "Query Parameters" /
  "Request Body schema" / "Response Schema".
- Every other level of a nested schema (e.g. an object nested inside an object nested inside an
  object). ReDoc alternates nested-schema backgrounds between the theme's
  `schema.nestedBackground` and a hardcoded `#ffffff`, so a deeply nested request/response schema
  flashes white partway down.
- The selected tab in the request/response sample panel (e.g. "Payload"). Its selected-state
  style pairs the theme's own light foreground color with a light background, so the label is
  invisible.

This package fixes all of those with a small `<style>` override, on top of a full dark `theme`
config (colors, sidebar, right panel, code blocks, typography, schema). You get a proper dark
ReDoc page with one method call instead of hand-rolling both.

## Quick start

```csharp
app.UseReDoc(options =>
{
    options.SpecUrl("/openapi/v1.json");
    options.UseDarkTheme();
});
```

That's it for the zero-config path. To customize the accent color:

```csharp
app.UseReDoc(options =>
{
    options.SpecUrl("/openapi/v1.json");
    options.UseDarkTheme(theme =>
    {
        theme.PrimaryColor = "#a78bfa"; // used for links, active sidebar items, the GET badge, etc.
        // theme.PrimaryColorLight / theme.PrimaryColorDark — optional, computed automatically if omitted
        // theme.AdditionalCss = ".my-rule { ... }"; // appended after the built-in overrides
    });
});
```

Everything outside the accent color (success/warning/error colors, grays, backgrounds, etc.) is
intentionally fixed — this is a themed package, not a general theming engine.

### Let viewers choose: Light/System/Dark theme selector

Not everyone on your team wants dark docs. Enable the selector and each viewer gets a small
Light/System/Dark toggle in the top-right corner. Light is ReDoc's stock theme, exactly as it
renders without this package. System follows the viewer's OS setting (`prefers-color-scheme`) and
switches live when the OS setting changes:

```csharp
app.UseReDoc(options =>
{
    options.SpecUrl("/openapi/v1.json");
    options.UseDarkTheme(theme => theme.EnableThemeSelector(defaultTheme: ReDocTheme.System));
});
```

`defaultTheme` (defaults to `Dark`) is what viewers see until they pick one. Their choice is then
remembered in the browser's `localStorage`. Switching re-renders ReDoc in place and keeps the
scroll position.

With the selector enabled, the dark CSS overrides are scoped under `html[data-redoc-theme="dark"]`.
`AdditionalCss` is not scoped and applies to both themes, so prefix your rules with that selector
if they should be dark-only.

### Fonts: no request to Google

ReDoc's page pulls Montserrat and Roboto from `fonts.googleapis.com` at runtime. `UseSystemFonts()`
renders the docs in the viewer's own OS fonts instead and removes that `<link>` from the page, so
it makes no third-party request at all:

```csharp
options.UseDarkTheme(theme => theme.UseSystemFonts());
```

That's one `style-src`/`font-src` entry you no longer need in a strict CSP, docs that render the
same on an air-gapped network, and no viewer IP addresses reaching Google — which LG München I
ruled a GDPR violation in 2022 (3 O 17493/20) when it happens without consent.

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

### Favicon

ReDoc's page declares no icon, so browsers fall back to `/favicon.ico`. To point it somewhere else:

```csharp
options.UseDarkTheme(theme => theme.Favicon = "/favicon.svg");
```

Use an absolute URL or a root-relative path — the pages are served at `/{route}/index.html`, so a
relative one would resolve against the page's own folder.

### Printing

Dark docs print badly: the page is either a black rectangle or near-white text on white paper,
depending on the browser's "background graphics" setting. `UseDarkTheme` therefore ships a print
stylesheet that prints the documentation black-on-white and leaves out the sidebar and this
package's own controls. Turn it off with `theme.PrintStyles = false`.

It only changes colors and hides page furniture. Sections that are collapsed on screen stay
collapsed on paper — ReDoc doesn't render their content until they're opened, so CSS can't reach
it.

## Multiple OpenAPI documents

`UseReDocDocuments` serves one ReDoc page per OpenAPI document, each with a picker in the top-right
corner for switching between them. It works on its own, and alongside the dark theme and the theme
selector:

```csharp
builder.Services.AddOpenApi("widgets");
builder.Services.AddOpenApi("manufacturers");

// ...

app.MapOpenApi();
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

app.MapGet("/widgets", GetWidgets).WithGroupName("widgets");
app.MapGet("/manufacturers", GetManufacturers).WithGroupName("manufacturers");
```

This serves `/api-docs/widgets/` and `/api-docs/manufacturers/`, and `/api-docs` redirects to the
first one. Each document reads its spec from `/openapi/{documentName}.json`, which is where
`MapOpenApi()` serves it.

Tag every endpoint with `.WithGroupName(...)`. By default, an endpoint without a group name appears
in every document.

| `ReDocDocument` | |
|---|---|
| `Name` | The OpenAPI document name, as passed to `AddOpenApi(name)`. |
| `DisplayName` | Label in the picker and the browser tab. Defaults to `Name`. |
| `SpecUrl` | This document's spec URL, if it isn't at `SpecUrlTemplate`. |
| `Route` | The page's path, e.g. `"reference/v2/taxes"`. Defaults to `{RoutePrefix}/{Name}`. |

| `ReDocDocumentsOptions` | |
|---|---|
| `RoutePrefix` | Where pages go by default. Defaults to `"api-docs"`. |
| `RedirectToFirstDocument` | Redirect `/{RoutePrefix}` to the first document. Defaults to `true`. |
| `SpecUrlTemplate` | Defaults to `"/openapi/{documentName}.json"`. |
| `ValidateDocumentNames` | Check at startup that every name is a registered document. Defaults to `true`. |
| `Links` | Plain entries listed after the documents, e.g. `new ReDocLink("Swagger UI", "~/swagger")`. |
| `ConfigureReDoc` | Runs for every page's `ReDocOptions`: theme, extra `HeadContent`, and so on. |

Picker links are relative, so they keep working under a `PathBase` or behind a reverse proxy. A
link URL that starts with `~/` is resolved the same way, against the app's base path. Other URLs
are used as-is.

To move a page, set its `Route`. Don't change `RoutePrefix` inside `ConfigureReDoc`: the pickers on
the other pages would link to the old path, so `UseReDocDocuments` throws if you do.

### Mistyped document names fail at startup

The list above has to mirror your `AddOpenApi(name)` calls. When the two drift, the page itself
still serves fine and only its spec request 404s — so the app starts clean, the server log says
nothing, and the mistake surfaces only when someone opens the page and finds ReDoc's "Something
went wrong…". `UseReDocDocuments` checks the names at startup instead and throws:

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

## Install

Pick the adapter for the ReDoc middleware you already use. On
[Swashbuckle.AspNetCore.ReDoc](https://www.nuget.org/packages/Swashbuckle.AspNetCore.ReDoc):

```bash
dotnet add package Hlibz.Redoc.Extensions.Swashbuckle
```

On [Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore):

```bash
dotnet add package Hlibz.Redoc.Extensions.RedocAspNetCore
```

Either way, `using Hlibz.Redoc.Extensions;` — the namespace, and everything in this README, is the
same on both. Each adapter brings in `Hlibz.Redoc.Extensions`, the host-agnostic engine, on its
own; install one, not both.

`Hlibz.Redoc.Extensions.RedocAspNetCore` replaces `Hlibz.Redoc.AspNetCore.Extensions`, which was
the same code built against one host — migrating is a package swap and a `using` change, with no
API change.

## Running the samples locally

```bash
dotnet run --project samples/Sample.RedocAspNetCore
```

This opens `http://localhost:5299/api-docs` in your browser automatically. It redirects to
`/api-docs/widgets/`, the page in the screenshot above, and the picker switches between the
sample's three documents. The Facilities page is configured with `UseSystemFonts()` and the other
two aren't, so switching to it shows the difference.

```bash
dotnet run --project samples/Sample.Swashbuckle
```

The same app on port 5399, against the other host. Both serve the same spec from `Sample.Shared`
and their `Program.cs` files are line-for-line the same, so running them together shows what, if
anything, differs between the hosts.

## Compatibility

| | |
|---|---|
| Library target | `net8.0` |
| Works in host apps targeting | net8.0, net9.0, net10.0 (NuGet TFM compatibility) |
| Requires | `Redoc.AspNetCore` ≥ 1.0.0, or `Swashbuckle.AspNetCore.ReDoc` ≥ 10.3.1 |
| Trimming | `IsAotCompatible` — this assembly raises no trim or AOT warnings |

The trimming row is a claim about this package only. ReDoc's config still travels through
`Redoc.AspNetCore`'s own reflection-based JSON serialization, which a fully NativeAOT host won't
run.

## Content Security Policy (CSP)

If your site doesn't send a strict `Content-Security-Policy` header, skip this section.

This package adds inline tags through `ReDocOptions.HeadContent`: a `<style>` for the dark theme,
a `<style>` and `<script>` for the theme selector, and a `<style>` and `<script>` for the document
picker on every `UseReDocDocuments` page. A strict policy blocks inline tags that don't carry a
nonce. The nonce recipe in the Redoc.AspNetCore README doesn't reach these tags, because it
rewrites the `IndexStream` template, and the middleware only fills in `%(HeadContent)` after that.
That recipe also misses Redoc.AspNetCore's own `<script type="text/javascript">` that calls
`Redoc.init`, so under a strict policy the page stays blank even without this package.

This variant covers everything. It fills in `HeadContent` itself, adds the nonce to every inline
tag, and hands the nonce to ReDoc for the styles it injects at runtime:

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
        string html = reader.ReadToEnd().Replace(
            "%(HeadContent)",
            $"<script>window.__webpack_nonce__ = '{nonce}';</script>\n{options.HeadContent}");

        // Add the nonce to every inline <script>/<style>. Tags with src= are covered by 'self'.
        html = Regex.Replace(
            html, @"<(script|style)\b(?![^>]*\bsrc=)", m => $"{m.Value} nonce=\"{nonce}\"");

        return new MemoryStream(Encoding.UTF8.GetBytes(html));
    };
});
```

With `UseReDocDocuments`, put the same code in `ConfigureReDoc`. The picker's tags are appended
to `HeadContent` after `ConfigureReDoc` runs, but the `IndexStream` above reads `HeadContent` per
request, so they get the nonce too.

ReDoc's bundle (2.0.0) also injects two static styles as it loads, and those can't take a nonce.
Allow them by hash in `style-src`: `'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='` and
`'sha256-DLDPR1ic47WIdK2WyeLkblb/tm2mQH+Jt/NNhZWu1k0='`. ReDoc also loads fonts from Google Fonts
(`fonts.googleapis.com` / `fonts.gstatic.com`) and the Redocly logo from `cdn.redoc.ly`, so allow
those hosts too or accept them being blocked. [`UseSystemFonts()`](#fonts-no-request-to-google)
removes the font request outright, so only `cdn.redoc.ly` is left.

## Contributing

Found a bug or have an idea for a feature? Feel free to
[open an issue](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/issues) or a
pull request. You can also reach me on [LinkedIn](https://www.linkedin.com/in/hzinchenko/).

## License

MIT — see [LICENSE](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/blob/main/LICENSE).
