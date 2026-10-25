using Hlibz.Redoc.AspNetCore.Extensions;
using Hlibz.Redoc.AspNetCore.Extensions.Sample.Models;
using Microsoft.AspNetCore.Http.HttpResults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
AddDocument(builder.Services, "widgets", "Widget Catalog API", "Browse and manage catalog widgets.");
AddDocument(builder.Services, "manufacturers", "Manufacturers API", "Companies that build widgets.");
AddDocument(builder.Services, "facilities", "Facilities API", "Plants where widgets are made.");

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

// The icon the pages point at, inline so the sample needs no wwwroot.
app.MapGet("/favicon.svg", () => TypedResults.Text(
    """
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">
      <rect width="16" height="16" rx="4" fill="#2a3142"/>
      <path d="M4 5h8M4 8h8M4 11h5" stroke="#38bdf8" stroke-width="1.6" stroke-linecap="round"/>
    </svg>
    """,
    "image/svg+xml")).ExcludeFromDescription();

app.MapGet("/widgets", () => TypedResults.Ok(Widget.Sample))
    .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
    .WithGroupName("widgets");

app.MapGet("/widgets/{id:guid}", Results<Ok<Widget>, NotFound<ErrorResponse>> (Guid id) =>
{
    Widget? widget = Widget.Sample.FirstOrDefault(w => w.Id == id);
    return widget is not null
        ? TypedResults.Ok(widget)
        : TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
})
    .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
    .WithGroupName("widgets");

app.MapPost("/widgets", Results<Created<Widget>, BadRequest<ValidationErrorResponse>> (Widget widget) =>
{
    if (widget.Price <= 0)
    {
        return TypedResults.BadRequest(new ValidationErrorResponse(
            "One or more fields failed validation.",
            new Dictionary<string, string[]> { ["price"] = ["Price must be greater than zero."] }));
    }

    return TypedResults.Created($"/widgets/{Guid.NewGuid()}", widget);
})
    .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
    .WithGroupName("widgets");

app.MapPut("/widgets/{id:guid}", Results<Ok<Widget>, NotFound<ErrorResponse>> (Guid id, Widget widget) =>
{
    return Widget.Sample.Any(w => w.Id == id)
        ? TypedResults.Ok(widget)
        : TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
})
    .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
    .WithGroupName("widgets");

app.MapDelete("/widgets/{id:guid}", Results<NoContent, NotFound<ErrorResponse>> (Guid id) =>
{
    return Widget.Sample.Any(w => w.Id == id)
        ? TypedResults.NoContent()
        : TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
})
    .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
    .WithGroupName("widgets");

IEnumerable<Manufacturer> manufacturers = Widget.Sample.Select(w => w.Manufacturer);

app.MapGet("/manufacturers", () => TypedResults.Ok(manufacturers))
    .WithGroupName("manufacturers");

app.MapGet("/manufacturers/{name}", Results<Ok<Manufacturer>, NotFound<ErrorResponse>> (string name) =>
{
    Manufacturer? manufacturer = manufacturers.FirstOrDefault(m => m.Name == name);
    return manufacturer is not null
        ? TypedResults.Ok(manufacturer)
        : TypedResults.NotFound(new ErrorResponse("No manufacturer has that name.", Guid.NewGuid().ToString()));
})
    .WithGroupName("manufacturers");

app.MapGet("/facilities", () => TypedResults.Ok(manufacturers.Select(m => m.Headquarters)))
    .WithGroupName("facilities");

app.Run();

// Endpoints without a group name land in every document, so each one is tagged via WithGroupName.
static void AddDocument(IServiceCollection services, string name, string title, string description)
{
    services.AddOpenApi(name, options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = title;
            document.Info.Description = description;
            return Task.CompletedTask;
        });
    });
}
