// The sample against Swashbuckle.AspNetCore.ReDoc. Deliberately line-for-line the same as
// Sample.RedocAspNetCore's — the adapters differ, the code a consumer writes doesn't — so the
// two can be run together (5299 and 5399) and compared. That comparison is the only way to see
// the hosts' bundled ReDoc versions apart: 2.0.0 there, 2.5.4 here.

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
