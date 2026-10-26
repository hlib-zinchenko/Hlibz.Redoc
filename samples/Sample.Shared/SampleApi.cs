// This is a plain class library rather than a web SDK project, so the minimal-API extension
// methods the samples get implicitly have to be imported by hand.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sample.Shared.Models;

namespace Sample.Shared;

/// <summary>
/// The three OpenAPI documents and the endpoints behind them, shared by every sample so each host
/// renders the <em>same</em> spec. That is what makes the samples comparable side by side, which is
/// how a rendering difference between the hosts' bundled ReDoc versions actually gets spotted.
/// </summary>
/// <remarks>
/// The models are shaped to exercise the theme's CSS overrides rather than to model a domain:
/// <c>Widget</c> nests five levels deep to surface ReDoc's alternating nested-schema background
/// bug, every <c>widgets</c> endpoint documents a <c>500</c> alongside its own negative response
/// with a distinct model, and every property carries a <c>[Description]</c>.
/// </remarks>
public static class SampleApi
{
    /// <summary>Registers the three documents the picker switches between.</summary>
    public static IServiceCollection AddSampleDocuments(this IServiceCollection services)
    {
        AddDocument(
            services, "widgets", "Widget Catalog API", "Browse and manage catalog widgets.");
        AddDocument(
            services, "manufacturers", "Manufacturers API", "Companies that build widgets.");
        AddDocument(services, "facilities", "Facilities API", "Plants where widgets are made.");
        AddDocument(
            services, "internal", "Internal Operations API", "Staff-only views of the supply chain.");
        return services;
    }

    /// <summary>
    /// Maps the sample's endpoints, plus the inline favicon the ReDoc pages point at so the sample
    /// needs no wwwroot.
    /// </summary>
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
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

        app.MapGet("/internal/facilities", () => TypedResults.Ok(manufacturers.Select(m => m.Headquarters)))
            .WithGroupName("internal");

        app.MapGet("/internal/manufacturers", () => TypedResults.Ok(manufacturers))
            .WithGroupName("internal");

        return app;
    }

    // Endpoints without a group name land in every document, so each one is tagged via WithGroupName.
    private static void AddDocument(
        IServiceCollection services, string name, string title, string description)
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
}
