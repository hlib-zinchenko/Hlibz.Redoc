# Hlibz.Redoc.Extensions.Core

The host-agnostic engine behind the Hlibz ReDoc extensions: the dark theme, the Light/System/Dark
selector, the font and print stylesheets, and the multi-document picker.

**This package on its own does nothing.** Install the one for your ReDoc host instead:

| | |
|---|---|
| [`Hlibz.Redoc.Extensions.Swashbuckle`](https://www.nuget.org/packages/Hlibz.Redoc.Extensions.Swashbuckle) | Dark theme and document picker for `Swashbuckle.AspNetCore.ReDoc`. |
| [`Hlibz.Redoc.Extensions.RedocAspNetCore`](https://www.nuget.org/packages/Hlibz.Redoc.Extensions.RedocAspNetCore) | The same, for `Redoc.AspNetCore`. Successor to `Hlibz.Redoc.AspNetCore.Extensions` 1.x. |
| [`Hlibz.Redoc.AspNetCore`](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore) | ReDoc 3 served by your own app: per-document authorization, `immutable` cache headers, a page with nothing inline. |

Each of those references this package itself, so you never add it by hand.

[Source, issues and docs on GitHub](https://github.com/hlib-zinchenko/Hlibz.Redoc)

MIT.
