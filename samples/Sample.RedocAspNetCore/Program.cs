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
        new ReDocDocument("manufacturers", "Manufacturers", Group: "Supply chain"),
        new ReDocDocument("facilities", "Facilities", Group: "Supply chain"),

        // Middleware carries no metadata, so nothing here can gate this page: Unlisted only hides
        // it from the other pages' pickers. Sample.Own shows the real gate, with RequireAuthorization.
        new ReDocDocument("internal", "Internal Operations", Route: "api-docs/internal/v1", Unlisted: true),
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

                // Assigned after the call to keep its other effects (no Google request) while
                // naming a stack of our own.
                theme.CodeFontFamily = "ui-monospace, Menlo, monospace";
            }

            // A brand accent with explicit shades, and raw CSS after the theme's own rules.
            if (document.Name == "manufacturers")
            {
                theme.PrimaryColor = "#a78bfa";
                theme.PrimaryColorLight = "#c4b5fd";
                theme.PrimaryColorDark = "#7c3aed";
                theme.AdditionalCss = "h1 { letter-spacing: 0.02em; }";
            }
        });
    });

app.MapGet("/", () => TypedResults.Redirect("/api-docs")).ExcludeFromDescription();
app.MapSampleEndpoints();

app.Run();
