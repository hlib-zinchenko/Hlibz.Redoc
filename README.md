# Hlibz.Redoc.AspNetCore.Extensions

A drop-in dark theme for [Redoc.AspNetCore](https://www.nuget.org/packages/Redoc.AspNetCore)'s
ReDoc UI middleware — one line to fix a handful of colors ReDoc hardcodes outside its own theme
config (scrollbar, selection, section captions, nested-schema backgrounds, sample-tab text), plus
a full dark color palette.

[![CI](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Hlibz.Redoc.AspNetCore.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore.Extensions)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hlibz.Redoc.AspNetCore.Extensions.svg)](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore.Extensions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

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

## Install

```bash
dotnet add package Hlibz.Redoc.AspNetCore.Extensions
```

## Running the sample locally

```bash
dotnet run --project samples/Hlibz.Redoc.AspNetCore.Extensions.Sample
```

This opens `http://localhost:5299` in your browser automatically — that's the screenshot above,
live.

## Compatibility

| | |
|---|---|
| Library target | `net8.0` |
| Works in host apps targeting | net8.0, net9.0, net10.0 (NuGet TFM compatibility) |
| Requires | `Redoc.AspNetCore` ≥ 1.0.0 |

## Content Security Policy (CSP)

If your site doesn't send a strict `Content-Security-Policy` header, skip this section.

This package adds an inline `<style>`, plus an inline `<script>` when the theme selector is
enabled, through `ReDocOptions.HeadContent`. A strict policy blocks inline tags that don't carry a
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

ReDoc's bundle (2.0.0) also injects two static styles as it loads, and those can't take a nonce.
Allow them by hash in `style-src`: `'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='` and
`'sha256-DLDPR1ic47WIdK2WyeLkblb/tm2mQH+Jt/NNhZWu1k0='`. ReDoc also loads fonts from Google Fonts
(`fonts.googleapis.com` / `fonts.gstatic.com`) and the Redocly logo from `cdn.redoc.ly`, so allow
those hosts too or accept them being blocked.

## Contributing

Found a bug or have an idea for a feature? Feel free to
[open an issue](https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions/issues) or a
pull request. You can also reach me on [LinkedIn](https://www.linkedin.com/in/hzinchenko/).

## License

MIT — see [LICENSE](LICENSE).
