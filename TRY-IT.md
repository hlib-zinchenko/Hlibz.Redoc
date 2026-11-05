# Try-it-out — investigation

`IDEAS.md` idea 8, looked at properly. Scope as asked: **JSON bodies and bearer auth, plus
something simple** (Basic and apiKey), built so other auth schemes and media types slot in later.
Everything below marked *measured* was checked against `samples/Sample.Own` (ReDoc 3.0.1) in a
browser on 2026-11-04. Everything else is a proposal.

## Status

The MVP is built on branch `feature/try-it`: `EnableTryIt()` on `Hlibz.Redoc.AspNetCore`, with the
scope below marked MVP. It was verified in `Sample.Own`: POST /widgets returned 401 without a token
and 201 with one, path parameters and missing required values behave as expected, a token carries
across operations, Escape and focus return work, and dark mode needed no extra rules. Decisions taken
on the open questions at the end of this file:

1. **Placement**: in `page-actions`, beside Copy.
2. **Auth entry**: in the drawer only, but per scheme, so it is entered once per page — or once
   per app, when `CredentialStorage` keeps it in the browser.
3. **Non-GET Send**: one click, with the button colored as a warning.
4. **Name**: `EnableTryIt()`.
5. **Scalar link recipe**: not written yet.

## Verdict

Doable, host-only, and lower-risk than `IDEAS.md` assumed. ReDoc 3 gives us a stable enough
per-operation hook (`data-section-id` + `data-testid="page-actions"`). The console can be **our
own DOM outside React's tree**: a `<dialog>` drawer that reads the spec itself and never touches
ReDoc's state. That keeps "nothing inline" and `script-src 'self'` intact, and the default CSP's
`connect-src 'self'` already allows same-origin requests. Rough size: one ~30–40 KB unminified
script plus CSS, served like `redoc-init.js`.

The adapters stay out of it. They run ReDoc 2.x, which has no `data-testid` and hashed class names
only, and they can only inline through `HeadContent`. It would be a second implementation against
a frozen bundle.

## What exists already

- **ReDoc 3.0.1 has nothing hidden to switch on** (*measured*, grep of the shipped bundle). "Try
  it" appears only as no-op telemetry (`sendTryItOpenedMessage`, `com.redocly.tryIt.opened`) and as
  the `x-hideTryItPanel` operation extension in its lint schema. Redocly's own post on 3.0 says Try
  It and "a handful of options that support it" stay commercial (Replay), and Replay can't be
  self-hosted.
- **`redoc-try`** (wll8) transplants Swagger UI's try-it into ReDoc 2.0.0-rc builds. Not a model
  for us: it adds a second 1 MB+ renderer, targets 2.x DOM, and Swagger UI under our CSP is its own
  project.
- **Idea 7 (a `Links` entry to Scalar or Swagger UI)** still costs nothing and should be documented
  either way. Try-it is the answer for people who want it *on the ReDoc page*.

## ReDoc 3's DOM, as far as we need it (*measured*)

- **Every operation is a section in one scrolling column**, rendered only near the viewport:
  `div[data-item-kind="httpItem"][data-section-id="/sample.own/paths/~1widgets~1{id}/get"]`.
  On first load, only 3 of the 5 sample operations existed in the DOM. The rest appeared as the
  view moved.
- **The section id is the operation's address**, in one of two forms. Without an `operationId`
  it is `/{tag-slug}/paths/{JSON-pointer path}/{method}`: take the part after `/paths/`, unescape
  `~1`→`/` and `~0`→`~`, split off the trailing method, and you have `spec.paths[path][method]`.
  **With one, it is `/{tag-slug}/{operationId}`, lowercased**, with `\` written as `%5C` and
  nothing else escaped — read off the bundle's slug function, and missed until an app whose every
  endpoint is named (`.WithName`) showed no buttons at all. The script indexes operationIds the
  same way and takes the longest one the id ends with, since an operationId may contain `/`.
  No guessing from headings either way.
- **Each section has its own `[data-testid="page-actions"]`** (the Copy split button). A button
  prepended there **survived** a colour-mode switch, sidebar navigation and scrolling with no
  remounts (5 mounts for 5 operations). A `MutationObserver` on `body` picked up the late-rendered
  sections. That is the same pattern the sidebar picker already relies on.
- **The URL changes with no `hashchange` or `popstate`** (`history.replaceState`/`pushState`).
  So "the current operation" can't come from URL events. That's fine: the button carries its own
  section id, so we never need to know.
- The right panel's payload example is in the DOM, but scraping it would tie us to markup. Generate
  examples from the spec instead.

## Proposed shape

### C# surface (host only)

```csharp
options.EnableTryIt();                       // off by default
options.EnableTryIt(tryIt =>
{
    tryIt.CredentialStorage = ReDocTryItCredentialStorage.Memory; // default; or Session, Local
});
```

The minimum is a toggle. Server side it is the existing machinery: `ReDocPage` emits
`<script type="module" src="redoc-tryit.js">`, and the console's CSS joins the page's own
stylesheet. *As built*, the script is served **beside each page**, inside that document's endpoint
group, not once under `_redoc/` as first proposed: a policy on a gated document has to cover its
console too, and `_redoc/` is shared by every page. What varies per page (spec URL, app base)
travels as `data-` attributes on the script tag, not in `redoc-init.js`.

### Client architecture

One script and four small parts. The registries are the extension seam:

1. **Spec**: `fetch` the same relative spec URL `redoc-init.js` passes to `init`, then parse it as
   JSON. The host always serves JSON. A gated page's spec carries the viewer's cookie, so this
   works with `RequireAuthorization` unchanged. Resolve local `$ref`s (`#/components/...`) lazily,
   with a cycle guard.
2. **Hook**: an observer mounts a "Try it" button into each `httpItem` section's `page-actions`.
   It skips `webhooks/` sections and operations with `x-hideTryItPanel: true`. That's Redocly's own
   extension, so specs written for Replay behave the same here for free.
3. **Drawer**: a native `<dialog>` appended to `body`, so we get focus handling, Escape and a
   backdrop free. It contains:
   - server (defaults below)
   - path, query and header parameters, prefilled from `example`/`default`/`enum[0]`
   - an auth block for the schemes the operation's `security` names
   - the body editor, from the body registry
   - Send
   - the response: status, time, headers, and the body pretty-printed when it's JSON

   Styled from ReDoc 3's own CSS variables, so dark mode, presets and `PrimaryColor` apply with
   no work.
4. **Registries** — the "extend later" part, kept internal for now:
   ```js
   auth.register('http:bearer', { fields: [token],           apply: (req, v) => header('Authorization', `Bearer ${v.token}`) });
   auth.register('http:basic',  { fields: [user, password],  apply: … });
   auth.register('apiKey',      { fields: [value],           apply: in header | query });
   body.register(/^application\/(.+\+)?json$/, { editor: textarea + example, encode: JSON text });
   ```
   Scheme key is `type` or `type:scheme`. A scheme with no handler shows "not supported here".
   `oauth2`/`openIdConnect` fall back to **"paste an access token"**, which is just the bearer
   handler. That covers most real use before any OAuth flow exists. A media type with no handler
   offers a raw text body with that `Content-Type`, rather than refusing.

Auth values are entered **once per security scheme, not per operation**: one bearer token
covers the whole page, keyed by scheme name.

### Where requests go

- No `servers` in the spec. That's what `Sample.Own` serves today (*measured*), and OpenAPI's
  default is `/`. Use the **app's base, derived from the page's own depth** (`../` per route
  segment, as the picker's hrefs already do). A bare `/` would be wrong under a `PathBase` or a
  path-rewriting proxy. The host knows each page's depth at startup, so this needs nothing per
  request.
- `servers` present: offer them in a select, and pre-select the first one on the page's own origin.
  Note that `Microsoft.AspNetCore.OpenApi` can emit the server's *bind* addresses, which are wrong
  behind a proxy. That is already documented for `ServeSpecs` (an `AddDocumentTransformer` fixes
  it).
- Cross-origin servers need two things the viewer can't see failing: the API's CORS, and
  `connect-src` in our CSP. `UseDefaultContentSecurityPolicy` says `'self'`. Same-origin is the
  supported case. Cross-origin gets a clear "blocked by CORS or CSP" message, because `fetch` can't
  tell the two apart, and a README line.

### Security, stated rather than chased

- **Credentials**: in memory by default. `CredentialStorage` opts in to `sessionStorage` or, at
  the user's request, `localStorage`, stored once per app so documents share them, with a Forget
  button and a `CredentialLifetime` (default 8 hours after the last change). Storing turns on the
  default CSP when the site set none. Encrypting the stored value was rejected: the page has to
  decrypt it unprompted, so the key would sit beside it. Nothing is logged. Password-type inputs,
  each with a show/hide eye.
- **Rendering**: response bodies and headers go in via `textContent` only. Spec descriptions are
  never rendered as Markdown in the drawer: the one shown, a response's, is its first line as plain
  text. A response is attacker-influenced data on our origin.
- **Cookies ride along**: a same-origin `fetch` sends the viewer's cookies. A viewer signed in to
  a gated docs page who presses Send on `DELETE` makes a real, authenticated call. That's what they
  asked for, but non-GET Send deserves a visibly different button. Expose `credentials: 'omit'` as
  an option if someone needs cookie-free calls.
- **CSP**: unchanged for the same-origin case, and nothing inline. This must stay true — the
  feature shouldn't cost the property the host is sold on.

## Scope

**MVP**:
- path, query and header parameters
- JSON request bodies, with an example generated from the schema: `example`, `examples`,
  `default`, `enum[0]` and `format`-aware placeholders; `allOf` merged, first of `oneOf`/`anyOf`;
  `readOnly` skipped
- bearer, Basic and apiKey (header or query), with oauth2/OIDC as paste-a-token
- same-origin requests
- a response viewer
- honouring `x-hideTryItPanel`

**Later, each a registry entry or a small option**:
- OAuth2 client-credentials, then authorization-code with PKCE (needs a redirect page we serve)
- apiKey in a cookie (not settable from `fetch`; would need `document.cookie` and same-site
  caveats)
- possibly a public `window`-level registration API, once the internal one has settled

**Since added**, after the MVP: copy as cURL; binary responses offered as a download, with every
response's size; the documented meaning of the returned status, and a flag when the operation
doesn't document it; Cancel; enum and boolean parameters as selects; and per-operation drafts, kept
in memory when the drawer closes, with a Reset to example for the body. Then
`application/x-www-form-urlencoded` (a registry entry: one `name=value` per line), a picker for a
media type's named `examples` (parameters take their first), and `SafeMethodsOnly`, which mounts
the button only on GET, HEAD, OPTIONS, TRACE and QUERY — a convenience for docs in front of a real
system, not access control. Then `multipart/form-data`, with a file picker per binary property
(found through `$ref` and `allOf`, the shape ASP.NET Core gives an `IFormFile` beside other form
fields), and a notice when `fetch` followed a redirect — otherwise a sign-in page's `200` was
matched against the operation's own and shown as success.

**Not doing**: the 2.x adapters, YAML specs (a YAML page renders, with no buttons, and the
README says so), `servers` variables beyond plain substitution.

## Testing

The C# suites can cover the server half: the file is served, the page links it only when enabled,
caching headers, and that the CSP still holds. They can't reach the half that matters: section-id
parsing, `$ref` resolution, example generation and request building.

That half now has `tests/Hlibz.Redoc.AspNetCore.BrowserTests`: Playwright for .NET driving the
samples' widget catalog in a real Chrome. It is a smoke test, not a catalogue — 401 then 201 on
POST /widgets, a multipart upload, a form's named examples, a followed redirect, a draft across
close and reopen — and it also catches the one real fragility: a ReDoc upgrade renaming
`page-actions` or `data-item-kind` leaves every test without a button to press. It uses the
system Chrome (GitHub's Ubuntu image has one), and installs Playwright's Chromium through the
package's own entry point only when there isn't one, so it needs no Node and no PowerShell.

`Sample.Shared` now has what the sample needed to exercise auth: the widget catalog's POST, PUT and
DELETE require a bearer token (any non-empty one) and declare a `bearer` scheme on just those
operations.

## Risks

- **DOM contract**: `data-item-kind`, `data-section-id` and `data-testid="page-actions"` aren't
  public API. They are named and test-oriented, though, so a rename breaks the feature loudly — no
  button appears — rather than silently. Pin to the bundle version, as the host already does.
- **Lazy rendering**: if a section is mounted and unmounted repeatedly, the observer must be cheap.
  Querying `httpItem` sections that lack our button costs nothing at sample scale. It's worth one
  check against a spec with a few hundred operations.
- **Scope creep**: every API has one more auth flow. The registry is there so "no" is cheap and
  "yes" doesn't mean a rewrite.

## Open questions

1. **Button placement**: in `page-actions` beside Copy (proposed, looks native), or in the right
   panel's request-sample header, where Swagger users look for it?
2. **Auth entry**: one page-wide "Authorize" control in the toolbar, plus the per-drawer block?
   Or only the drawer block?
3. **Default on non-GET**: allow Send outright, or ask for a second click?
4. **Name**: `EnableTryIt()` / `TryIt`, or match Redocly's "Replay"? (Probably not — it's their
   product name.)
5. Before building, should **idea 7's Scalar link recipe** go in the README? It's an afternoon and
   answers the same request today.
