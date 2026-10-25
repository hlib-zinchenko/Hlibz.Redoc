// The sample against Redoc.AspNetCore. Everything that isn't host wiring — the three documents
// and the endpoints behind them — lives in Sample.Shared, so this and Sample.Swashbuckle render
// the same spec and can be compared side by side.

using Hlibz.Redoc.Extensions;
using Sample.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSampleDocuments();

WebApplication app = builder.Build();

app.MapOpenApi();
app.UseReDocDocuments(
    [
        new ReDocDocument("widgets", "Widget Catalog"),
        new ReDocDocument("manufacturers", "Manufacturers"),
        new ReDocDocument("facilities", "Facilities"),
    ],
    docs =>
    {
        docs.RoutePrefix = "api-docs";
        docs.Links.Add(new ReDocLink("Widgets OpenAPI JSON", "~/openapi/widgets.json"));
        docs.ConfigureReDoc = (redoc, document) => redoc.UseDarkTheme(theme =>
        {
            theme.EnableThemeSelector(defaultTheme: ReDocTheme.System);
            theme.Favicon = "/favicon.svg";

            // Only one of the three pages, so the difference is visible by switching documents:
            // this one renders in the viewer's OS fonts and makes no request to Google's.
            if (document.Name == "facilities")
            {
                theme.UseSystemFonts();
            }
        });
    });

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
