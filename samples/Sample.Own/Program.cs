// The sample against this repo's own host — no Redoc.AspNetCore, no Swashbuckle, ReDoc 3 served
// from the package itself. It renders the same three documents as the other two samples
// (Sample.Shared), so running them side by side (5299, 5399, 5499) shows what owning the page
// buys: ReDoc 3 instead of 2.x, pages behind endpoint routing, and a page with nothing inline.

using Hlibz.Redoc.Extensions;
using Sample.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSampleDocuments();

WebApplication app = builder.Build();

app.MapOpenApi();
app.MapReDocDocuments(
    [
        // Grouped so the picker shows an <optgroup>: the catalogue on its own, the two
        // supply-side documents together.
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers", Group: "Supply chain"),
        new ReDocDocument("facilities", "Facilities", Group: "Supply chain"),
    ],
    docs =>
    {
        docs.RoutePrefix = "api-docs";
        docs.Links.Add(new ReDocLink("Widgets OpenAPI JSON", "~/openapi/widgets.json"));
        docs.DefaultColorMode = ReDocColorMode.System;

        // The pages are served under a real policy, so the sample proves it rather than the
        // README claiming it. 'unsafe-inline' is in style-src because ReDoc renders through
        // styled-components and injects its rules at runtime; everything else is 'self' because
        // this package puts no script, style or config in the markup.
        docs.UseDefaultContentSecurityPolicy();

        // A brand accent, one hue per color mode, to show both properties doing something.
        docs.PrimaryColor = "#c026d3";
        docs.DarkModePrimaryColor = "#e879f9";

        // A named palette for dark mode, on top of which those two accents still win — which is
        // the rule worth seeing rather than reading: the preset paints the greys, the brand paints
        // the accent. Switch to Nord or GitHubDark to see the other two.
        docs.ThemePreset = ReDocThemePreset.Midnight;

        // The two controls are placed separately, and the sample splits them to show it: the
        // picker goes where navigation already is, and the link button to the other corner, clear
        // of both the page title and ReDoc's own buttons above the content. Set them to the same
        // placement and they share one strip instead. On a phone, where ReDoc hides its sidebar
        // and owns the bottom-right corner, both come back out over the page on their own.
        docs.PickerPlacement = ReDocToolbarPlacement.Sidebar;
        docs.ToolbarItemsPlacement = ReDocToolbarPlacement.BottomRight;

        // Three documents don't need search, but the sample is where it gets exercised: typing
        // "supply" finds both documents in that group without either being named for it.
        docs.SearchablePicker = true;

        // A toolbar button beside the picker, and the prefix sending you back to whichever
        // document you were last reading.
        docs.AddToolbarItem("Sample source", "https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions", newTab: true);
        docs.RememberLastDocument = true;
    });

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
