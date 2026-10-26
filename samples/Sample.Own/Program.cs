// The sample against this repo's own host — no Redoc.AspNetCore, no Swashbuckle, ReDoc 3 served
// from the package itself. It renders the same three documents as the other two samples
// (Sample.Shared), so running them side by side (5299, 5399, 5499) shows what owning the page
// buys: ReDoc 3 instead of 2.x, pages behind endpoint routing, and a page with nothing inline.

using System.Security.Claims;
using Hlibz.Redoc.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sample.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSampleDocuments();

// A cookie scheme and one policy, so the "internal" document below can be gated for real: an
// unauthenticated request to it is sent to /sign-in, and after signing in the page renders.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(cookie => cookie.LoginPath = "/sign-in");
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Employees", policy => policy.RequireClaim("employee", "true"));

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// No MapOpenApi() on purpose: with ServeSpecs below, each page serves its own document from its
// own route, so the only way to the internal spec is through the policy that guards its page.
// Mapping it as well would reopen /openapi/internal.json to anyone, which is the hole this sample
// exists to show closed.
ReDocDocumentsConventionBuilder docs = app.MapReDocDocuments(
    [
        // Grouped so the picker shows an <optgroup>: the catalogue on its own, the two
        // supply-side documents together.
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers", Group: "Supply chain"),
        new ReDocDocument("facilities", "Facilities", Group: "Supply chain"),

        // Gated below. Unlisted keeps it out of the public pages' picker (it hides the link and
        // protects nothing; the policy does that), and a nested Route shows the picker's relative
        // hrefs holding up at a deeper path.
        new ReDocDocument("internal", "Internal Operations", Route: "api-docs/internal/v1", Unlisted: true),
    ],
    options =>
    {
        options.RoutePrefix = "api-docs";

        // Each page serves its own document at openapi.json beside it, so the spec is one of that
        // document's endpoints and the policy below reaches it. Nothing else needs mapping: the
        // document comes from the provider AddOpenApi registers, not from MapOpenApi's endpoint.
        options.ServeSpecs = true;
        options.Links.Add(
            new ReDocLink("Widgets OpenAPI JSON", "~/api-docs/widgets/openapi.json"));
        options.DefaultColorMode = ReDocColorMode.System;
        options.Favicon = "/favicon.svg";

        // Not surfaced as properties of their own; AdditionalCss reaches any ReDoc 3 variable and
        // AdditionalConfigJson any ReDoc option. Both are validated when set.
        options.RequiredPropsFirst = true;
        options.AdditionalConfigJson = """{"showExtensions": true}""";
        options.AdditionalCss = ":root { --border-radius: 6px; }";

        // Left at their defaults on purpose: EnableTelemetry (calls a third party),
        // HideDownloadButton, PrintStyles = false, RedirectToFirstDocument = false and
        // ValidateDocumentNames = false each switch something off, and SpecUrlTemplate / SpecUrl
        // are what ServeSpecs replaces — setting the template as well throws, since the two can't
        // both decide where a page reads its spec from.

        // The pages are served under a real policy, so the sample proves it rather than the
        // README claiming it. 'unsafe-inline' is in style-src because ReDoc renders through
        // styled-components and injects its rules at runtime; everything else is 'self' because
        // this package puts no script, style or config in the markup.
        options.UseDefaultContentSecurityPolicy();

        // A brand accent, one hue per color mode, to show both properties doing something.
        options.PrimaryColor = "#c026d3";
        options.DarkModePrimaryColor = "#e879f9";

        // A named palette for dark mode, on top of which those two accents still win — which is
        // the rule worth seeing rather than reading: the preset paints the greys, the brand paints
        // the accent. Switch to Nord or GitHubDark to see the other two.
        options.DarkThemePreset = ReDocDarkThemePreset.Midnight;

        // The two controls are placed separately, and the sample splits them to show it: the
        // picker goes where navigation already is, and the link button to the other corner, clear
        // of both the page title and ReDoc's own buttons above the content. Set them to the same
        // placement and they share one strip instead. On a phone, where ReDoc hides its sidebar
        // and owns the bottom-right corner, both come back out over the page on their own.
        options.PickerPlacement = ReDocToolbarPlacement.Sidebar;
        options.ToolbarItemsPlacement = ReDocToolbarPlacement.BottomRight;

        // Three documents don't need search, but the sample is where it gets exercised: typing
        // "supply" finds both documents in that group without either being named for it.
        options.SearchablePicker = true;

        // A toolbar button beside the picker, and the prefix sending you back to whichever
        // document you were last reading.
        options.AddToolbarItem("Sample source", "https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions", newTab: true);
        options.RememberLastDocument = true;

        // A plain GET link, so the policy needs no form-action; it is how you reach the gated page.
        options.AddToolbarItem("Sign in (internal docs)", "~/sign-in");
    });

// One call, and the internal document is private: the policy goes on that document's own
// endpoints — its page, its scripts and its spec — and not on the shared ReDoc bundle, which is
// ReDoc's own JavaScript and would blank every other page if it were gated. Anonymous requests to
// /api-docs/internal/v1/ and to its openapi.json both end up at /sign-in.
docs.ForDocument("internal").RequireAuthorization("Employees");

// Signs in a made-up employee and sends them back to where they were headed. Only a local path is
// followed, so this can't be turned into an open redirect.
app.MapGet("/sign-in", async (HttpContext context, string? returnUrl) =>
{
    ClaimsIdentity identity = new(
        [new Claim(ClaimTypes.Name, "Sample Employee"), new Claim("employee", "true")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(new ClaimsPrincipal(identity));
    return TypedResults.LocalRedirect(Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        && returnUrl!.StartsWith('/') && !returnUrl.StartsWith("//")
            ? returnUrl
            : "/api-docs/internal/v1/");
}).ExcludeFromDescription();

app.MapGet("/sign-out", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return TypedResults.LocalRedirect("/api-docs");
}).ExcludeFromDescription();

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
