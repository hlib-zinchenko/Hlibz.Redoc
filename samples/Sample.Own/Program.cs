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

        // A toolbar button beside the picker, and the prefix sending you back to whichever
        // document you were last reading.
        docs.AddToolbarItem("Sample source", "https://github.com/hlib-zinchenko/Hlibz.Redoc.AspNetCore.Extensions", newTab: true);
        docs.RememberLastDocument = true;
    });

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
