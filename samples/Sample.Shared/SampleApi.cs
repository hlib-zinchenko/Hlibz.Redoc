// This is a plain class library rather than a web SDK project, so the minimal-API extension
// methods the samples get implicitly have to be imported by hand.
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
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
public static partial class SampleApi
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

        // Sends an ETag and honours If-None-Match, which the spec doesn't list as a parameter —
        // the usual case of a header an API takes that its document leaves out, and what the
        // try-it console's extra headers are for.
        app.MapGet("/widgets/{id:guid}", Results<Ok<Widget>, NotFound<ErrorResponse>, StatusCodeHttpResult> (Guid id, HttpContext context) =>
        {
            Widget? widget = Widget.Sample.FirstOrDefault(w => w.Id == id);
            if (widget is null)
            {
                return TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
            }

            string etag = $"\"{widget.Id:N}-1\"";
            context.Response.Headers.ETag = etag;
            return context.Request.Headers.IfNoneMatch.ToString() == etag
                ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
                : TypedResults.Ok(widget);
        })
            .Produces(StatusCodes.Status304NotModified)
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
            .RequireSampleToken()
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                if (operation.RequestBody?.Content?.TryGetValue(
                    "application/json", out OpenApiMediaType? json) == true)
                {
                    json.Examples = NewWidgetExamples();
                }

                return Task.CompletedTask;
            })
            .WithGroupName("widgets");

        app.MapPut("/widgets/{id:guid}", Results<Ok<Widget>, NotFound<ErrorResponse>> (Guid id, Widget widget) =>
        {
            return Widget.Sample.Any(w => w.Id == id)
                ? TypedResults.Ok(widget)
                : TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
        })
            .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
            .RequireSampleToken()
            .WithGroupName("widgets");

        app.MapDelete("/widgets/{id:guid}", Results<NoContent, NotFound<ErrorResponse>> (Guid id) =>
        {
            return Widget.Sample.Any(w => w.Id == id)
                ? TypedResults.NoContent()
                : TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
        })
            .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
            .RequireSampleToken()
            .WithGroupName("widgets");

        // Two form bodies, so the try-it console's form editors have something real to send: a
        // review as plain form fields, and a photo as a multipart upload. Antiforgery is off
        // because these are API endpoints a token protects, not forms a page of ours renders.
        app.MapPost("/widgets/{id:guid}/reviews", Results<Created<WidgetReview>, NotFound<ErrorResponse>, BadRequest<ValidationErrorResponse>> (Guid id, [FromForm] WidgetReview review) =>
        {
            if (!Widget.Sample.Any(w => w.Id == id))
            {
                return TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
            }

            if (review.Rating is < 1 or > 5)
            {
                return TypedResults.BadRequest(new ValidationErrorResponse(
                    "One or more fields failed validation.",
                    new Dictionary<string, string[]> { ["rating"] = ["Rating must be from 1 to 5."] }));
            }

            return TypedResults.Created($"/widgets/{id}/reviews/{Guid.NewGuid()}", review);
        })
            .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
            .RequireSampleToken()
            .DisableAntiforgery()
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                // Every form media type the generator offered gets the same examples.
                foreach (OpenApiMediaType media in operation.RequestBody?.Content?.Values ?? [])
                {
                    media.Examples = ReviewExamples();
                }

                return Task.CompletedTask;
            })
            .WithGroupName("widgets");

        app.MapPost("/widgets/{id:guid}/photos", Results<Created<PhotoReceipt>, NotFound<ErrorResponse>, BadRequest<ValidationErrorResponse>> (Guid id, IFormFile photo, [FromForm] string? caption) =>
        {
            if (!Widget.Sample.Any(w => w.Id == id))
            {
                return TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
            }

            if (!photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.BadRequest(new ValidationErrorResponse(
                    "One or more fields failed validation.",
                    new Dictionary<string, string[]> { ["photo"] = [$"Expected an image, got {photo.ContentType}."] }));
            }

            // Nothing is stored: the sample only says what arrived.
            return TypedResults.Created(
                $"/widgets/{id}/photos/{Guid.NewGuid()}",
                new PhotoReceipt(id, photo.FileName, photo.ContentType, photo.Length, caption));
        })
            .Produces<InternalErrorResponse>(StatusCodes.Status500InternalServerError)
            .RequireSampleToken()
            .DisableAntiforgery()
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                // Only the text fields have examples; a file can't be one.
                foreach (OpenApiMediaType media in operation.RequestBody?.Content?.Values ?? [])
                {
                    media.Examples = new Dictionary<string, IOpenApiExample>
                    {
                        ["captioned"] = new OpenApiExample
                        {
                            Summary = "With a caption",
                            Value = new JsonObject { ["caption"] = "Front view, studio lighting" },
                        },
                        ["bare"] = new OpenApiExample
                        {
                            Summary = "Just the file",
                            Value = new JsonObject(),
                        },
                    };
                }

                return Task.CompletedTask;
            })
            .WithGroupName("widgets");

        MapRequestShapes(app);

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

    /// <summary>
    /// Named request examples for <c>POST /widgets</c>, so ReDoc's request panel and the try-it
    /// console's example picker have more than one to switch between. Two are accepted and one is
    /// the 400 the endpoint answers for a price of zero, so both outcomes are a click away.
    /// Serialized with the web defaults, as the endpoint itself reads them.
    /// </summary>
    private static Dictionary<string, IOpenApiExample> NewWidgetExamples()
    {
        Widget keyboard = Widget.Sample.First();
        Widget desk = Widget.Sample.Last();

        return new Dictionary<string, IOpenApiExample>
        {
            ["keyboard"] = new OpenApiExample
            {
                Summary = "A complete widget",
                Description = "Every field filled in, including where it ships from.",
                Value = JsonSerializer.SerializeToNode(keyboard, JsonSerializerOptions.Web),
            },
            ["unassigned"] = new OpenApiExample
            {
                Summary = "Not in a warehouse yet",
                Description = "A draft with no warehouse assigned: `warehouse` is null.",
                Value = JsonSerializer.SerializeToNode(desk, JsonSerializerOptions.Web),
            },
            ["freePrice"] = new OpenApiExample
            {
                Summary = "Rejected: a price of zero",
                Description = "Fails validation, so the endpoint answers 400.",
                Value = JsonSerializer.SerializeToNode(
                    keyboard with { Name = "Free Keyboard", Price = 0m },
                    JsonSerializerOptions.Web),
            },
        };
    }

    /// <summary>
    /// Named examples for posting a review as a form: plain fields, a repeated field, and the 400
    /// a rating out of range gets.
    /// </summary>
    private static Dictionary<string, IOpenApiExample> ReviewExamples() => new()
    {
        ["glowing"] = new OpenApiExample
        {
            Summary = "Five stars",
            Value = new JsonObject { ["rating"] = 5, ["comment"] = "Types like a dream." },
        },
        ["tagged"] = new OpenApiExample
        {
            Summary = "With tags",
            Description = "`tags` is repeated once per value, as a form sends an array.",
            Value = new JsonObject
            {
                ["rating"] = 4,
                ["comment"] = "Loud, but worth it.",
                ["tags"] = new JsonArray("clicky", "sturdy"),
            },
        },
        ["outOfRange"] = new OpenApiExample
        {
            Summary = "Rejected: a rating of 9",
            Description = "Fails validation, so the endpoint answers 400.",
            Value = new JsonObject { ["rating"] = 9 },
        },
    };

    /// <summary>
    /// Answers 401 unless the request carries a bearer token. Any non-empty token passes: this
    /// sample has nothing to check one against, and the point is a protected endpoint for the
    /// try-it console to send credentials to.
    /// </summary>
    private static RouteHandlerBuilder RequireSampleToken(this RouteHandlerBuilder endpoint) =>
        endpoint
            .AddEndpointFilter(async (context, next) =>
            {
                string? header = context.HttpContext.Request.Headers.Authorization;
                if (header is null
                    || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    || header.Length <= "Bearer ".Length)
                {
                    context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
                    return TypedResults.Unauthorized();
                }

                return await next(context);
            })
            .WithMetadata(new SampleAuth(BearerScheme))
            .Produces(StatusCodes.Status401Unauthorized);

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

            // Declared only where an endpoint actually asks for credentials, so the documents
            // without any stay free of a scheme nothing uses.
            options.AddOperationTransformer((operation, context, _) =>
            {
                SampleAuth? auth = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<SampleAuth>().FirstOrDefault();
                if (auth is null)
                {
                    return Task.CompletedTask;
                }

                OpenApiDocument document = context.Document!;
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??=
                    new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[auth.Scheme] = SchemeFor(auth.Scheme);

                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(auth.Scheme, document)] = [],
                    },
                ];
                return Task.CompletedTask;
            });
        });
    }

    /// <summary>Marks an endpoint protected by one of the sample's three schemes.</summary>
    private sealed record SampleAuth(string Scheme);
}
