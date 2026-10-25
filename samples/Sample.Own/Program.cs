// The sample against this repo's own host — no Redoc.AspNetCore, no Swashbuckle, ReDoc 3 served
// from the package itself. It renders the same three documents as the other two samples
// (Sample.Shared), so running them side by side (5299, 5399, 5499) shows what owning the page
// buys: ReDoc 3 instead of 2.x, pages behind endpoint routing, and a page with nothing inline.

using Hlibz.Redoc.Extensions;
using Sample.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSampleDocuments();

WebApplication app = builder.Build();

// The policy these pages are actually served under, so the sample proves it rather than the
// README claiming it. 'unsafe-inline' is in style-src because ReDoc renders through
// styled-components and injects its rules at runtime; everything else is 'self' because this
// package puts no script, style or config in the markup.
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: https://cdn.redoc.ly; font-src 'self' data:; "
        + "worker-src 'self' blob:; child-src 'self' blob:; connect-src 'self'; "
        + "object-src 'none'; base-uri 'none'";
    await next(context);
});

app.MapOpenApi();
app.MapReDocDocuments(
    [
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers"),
        new ReDocDocument("facilities", "Facilities"),
    ],
    docs =>
    {
        docs.RoutePrefix = "api-docs";
        docs.Links.Add(new ReDocLink("Widgets OpenAPI JSON", "~/openapi/widgets.json"));
        docs.DefaultColorMode = ReDocColorMode.System;
    });

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
