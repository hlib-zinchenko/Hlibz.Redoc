using System.Text;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;
using Sample.Shared.Models;

namespace Sample.Shared;

/// <summary>
/// Widget-catalog operations shaped to cover what the try-it console can send, each one a request
/// a person can check by eye in Sample.Own: query parameters in four styles and a path parameter
/// in matrix style, a response in two media types, a whole-file upload, a server with a variable,
/// and API-key and Basic auth beside the bearer token the write operations take. The browser tests
/// drive these same operations.
/// </summary>
/// <remarks>
/// Where the wire format is the point, the response carries the request target as it
/// <c>received</c> it, still percent-encoded, so what the console sent can be read back off it.
/// </remarks>
public static partial class SampleApi
{
    private const string BearerScheme = "bearer";
    private const string ApiKeyScheme = "partnerKey";
    private const string BasicScheme = "supplier";

    private static readonly string[] Regions = ["eu", "us"];

    private static void MapRequestShapes(IEndpointRouteBuilder app)
    {
        // Enum and boolean parameters as drop-downs, an array sent pipe-delimited and an object
        // sent as deepObject (price[min]=100). Named, so ReDoc anchors it by operationId.
        app.MapGet("/widgets/search", (HttpContext context) =>
        {
            IQueryCollection query = context.Request.Query;
            IEnumerable<Widget> found = Widget.Sample;
            if (Enum.TryParse(query["status"], ignoreCase: true, out WidgetStatus status))
            {
                found = found.Where(w => w.Status == status);
            }

            if (bool.TryParse(query["shipping"], out bool shipping))
            {
                found = found.Where(w => (w.Warehouse is not null) == shipping);
            }

            string[] tags = query["tags"].ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (tags.Length > 0)
            {
                found = found.Where(w => w.Tags.Intersect(tags).Any());
            }

            if (decimal.TryParse(query["price[min]"], out decimal min))
            {
                found = found.Where(w => w.Price >= min);
            }

            if (decimal.TryParse(query["price[max]"], out decimal max))
            {
                found = found.Where(w => w.Price <= max);
            }

            string? name = query["name"];
            if (!string.IsNullOrEmpty(name))
            {
                found = found.Where(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }

            if (int.TryParse(query["limit"], out int limit) && limit > 0)
            {
                found = found.Take(limit);
            }

            return TypedResults.Ok(new WidgetSearchResult(RawTarget(context), [.. found]));
        })
            .WithName("SearchWidgets")
            .WithSummary("Search widgets")
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Parameters =
                [
                    // Constraints the console checks as you type, warning rather than refusing.
                    new OpenApiParameter
                    {
                        Name = "name", In = ParameterLocation.Query,
                        Description = "Part of the widget's name, 2 to 40 characters.",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String, MinLength = 2, MaxLength = 40,
                        },
                        Example = "Keyboard",
                    },
                    new OpenApiParameter
                    {
                        Name = "limit", In = ParameterLocation.Query,
                        Description = "At most this many widgets, from 1 to 50.",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "50",
                        },
                        Example = 10,
                    },
                    new OpenApiParameter
                    {
                        Name = "status", In = ParameterLocation.Query,
                        Description = "Only widgets in this lifecycle status.",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Enum = [.. Enum.GetNames<WidgetStatus>().Select(n => (JsonNode)n)],
                        },
                        Example = "Active",
                    },
                    new OpenApiParameter
                    {
                        Name = "shipping", In = ParameterLocation.Query,
                        Description = "Only widgets that have (or haven't) been assigned a warehouse.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                        Example = true,
                    },
                    new OpenApiParameter
                    {
                        Name = "tags", In = ParameterLocation.Query,
                        Description = "Widgets carrying any of these tags, sent as tags=a|b.",
                        Style = ParameterStyle.PipeDelimited, Explode = false,
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Enum = [.. Widget.Sample.SelectMany(w => w.Tags).Distinct()
                                    .Select(t => (JsonNode)t)],
                            },
                        },
                        Example = new JsonArray("electronics", "furniture"),
                    },
                    new OpenApiParameter
                    {
                        Name = "price", In = ParameterLocation.Query,
                        Description = "A price range, sent as price[min]=…&price[max]=….",
                        Style = ParameterStyle.DeepObject, Explode = true,
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["min"] = new OpenApiSchema { Type = JsonSchemaType.Number },
                                ["max"] = new OpenApiSchema { Type = JsonSchemaType.Number },
                            },
                        },
                        Example = new JsonObject { ["min"] = 100, ["max"] = 400 },
                    },
                ];
                return Task.CompletedTask;
            });

        // A path parameter in matrix style: /widgets/compare/;ids=a,b.
        app.MapGet("/widgets/compare/{ids}", (HttpContext context, string ids) =>
        {
            string list = ids.StartsWith(";ids=", StringComparison.Ordinal) ? ids[5..] : ids;
            HashSet<Guid> wanted = [.. list.Split(',').Select(v => Guid.TryParse(v, out Guid id) ? id : Guid.Empty)];
            return TypedResults.Ok(new WidgetSearchResult(
                RawTarget(context), [.. Widget.Sample.Where(w => wanted.Contains(w.Id))]));
        })
            .WithSummary("Compare widgets side by side")
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Parameters =
                [
                    new OpenApiParameter
                    {
                        Name = "ids", In = ParameterLocation.Path, Required = true,
                        Description = "The widgets to compare, sent as ;ids=a,b.",
                        Style = ParameterStyle.Matrix, Explode = false,
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                        },
                        Example = new JsonArray(
                            [.. Widget.Sample.Select(w => (JsonNode)w.Id.ToString())]),
                    },
                ];
                return Task.CompletedTask;
            });

        // The catalog as JSON or CSV, chosen by Accept, behind a partner API key.
        app.MapGet("/widgets/export", (HttpContext context) =>
        {
            if (!context.Request.Headers.Accept.ToString().Contains("text/csv"))
            {
                return Results.Json(Widget.Sample);
            }

            StringBuilder csv = new("id,name,price,status\n");
            foreach (Widget w in Widget.Sample)
            {
                csv.Append(FormattableString.Invariant($"{w.Id},{w.Name},{w.Price},{w.Status}\n"));
            }

            return Results.Text(csv.ToString(), "text/csv");
        })
            .WithSummary("Export the catalog")
            // One Produces with both types: a second call for the same status replaces the first.
            .Produces<IReadOnlyCollection<Widget>>(
                StatusCodes.Status200OK, "application/json", "text/csv")
            .RequireSampleCredential(ApiKeyScheme)
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                // The CSV is text, not the JSON array's schema.
                if (operation.Responses?.TryGetValue("200", out IOpenApiResponse? ok) == true
                    && ok.Content?.TryGetValue("text/csv", out OpenApiMediaType? csv) == true)
                {
                    csv.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
                    csv.Example = "id,name,price,status\n" + Widget.Sample.First().Id
                        + ",Mechanical Keyboard,129.99,Active";
                }

                return Task.CompletedTask;
            });

        // A whole-file body: the datasheet itself, as a PDF or a scanned image, behind Basic auth.
        app.MapPut("/widgets/{id:guid}/datasheet", async Task<Results<Ok<DatasheetReceipt>, NotFound<ErrorResponse>>> (Guid id, HttpRequest request) =>
        {
            if (!Widget.Sample.Any(w => w.Id == id))
            {
                return TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
            }

            // Nothing is stored: the sample only says what arrived.
            using MemoryStream body = new();
            await request.Body.CopyToAsync(body);
            return TypedResults.Ok(new DatasheetReceipt(id, request.ContentType ?? string.Empty, body.Length));
        })
            .WithSummary("Upload a widget's datasheet")
            .RequireSampleCredential(BasicScheme)
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Description = "The datasheet file itself, as the whole body.",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/pdf"] = new()
                        {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                        },
                        ["image/*"] = new(),
                    },
                };
                return Task.CompletedTask;
            });

        // A polymorphic body: ASP.NET Core describes Promotion as an anyOf with a discriminator,
        // so the console offers each kind as a variant to start from.
        app.MapPost("/widgets/{id:guid}/promotions", Results<Created<Promotion>, NotFound<ErrorResponse>, BadRequest<ValidationErrorResponse>> (Guid id, Promotion promotion) =>
        {
            if (!Widget.Sample.Any(w => w.Id == id))
            {
                return TypedResults.NotFound(new ErrorResponse("No widget matches the given id.", Guid.NewGuid().ToString()));
            }

            if (promotion is PercentOffPromotion { Percent: < 1 or > 99 })
            {
                return TypedResults.BadRequest(new ValidationErrorResponse(
                    "One or more fields failed validation.",
                    new Dictionary<string, string[]> { ["percent"] = ["Percent must be from 1 to 99."] }));
            }

            return TypedResults.Created($"/widgets/{id}/promotions/{Guid.NewGuid()}", promotion);
        })
            .WithSummary("Start a promotion")
            .RequireSampleToken()
            .WithGroupName("widgets");

        // Deprecated in favour of search, and marked so: ReDoc badges it, and the console says so
        // at the top of its panel.
        app.MapGet("/widgets/by-name/{name}", Results<Ok<Widget>, NotFound<ErrorResponse>> (string name) =>
        {
            Widget? widget = Widget.Sample.FirstOrDefault(
                w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
            return widget is not null
                ? TypedResults.Ok(widget)
                : TypedResults.NotFound(new ErrorResponse("No widget has that name.", Guid.NewGuid().ToString()));
        })
            .WithSummary("Find a widget by its exact name")
            .WithDescription("Deprecated: use **Search widgets** with `name`, which matches part of a name.")
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Deprecated = true;
                foreach (IOpenApiParameter parameter in operation.Parameters ?? [])
                {
                    if (parameter is OpenApiParameter { Name: "name" } concrete)
                    {
                        concrete.Example = "Mechanical Keyboard";
                    }
                }

                return Task.CompletedTask;
            });

        // Served per region, so the operation declares a server with a {region} variable. The
        // documented path answers too, with every region added up, through the second server.
        app.MapGet("/widgets/availability", () =>
                TypedResults.Ok(new Availability("all", Regions.Sum(UnitsIn))))
            .WithSummary("Units available to ship")
            .WithGroupName("widgets")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Servers =
                [
                    new OpenApiServer
                    {
                        Url = "/{region}",
                        Description = "One region",
                        Variables = new Dictionary<string, OpenApiServerVariable>
                        {
                            ["region"] = new()
                            {
                                Default = Regions[0],
                                Enum = [.. Regions],
                                Description = "The region whose warehouses answer.",
                            },
                        },
                    },
                    new OpenApiServer { Url = "/", Description = "Every region" },
                ];
                return Task.CompletedTask;
            });

        foreach (string region in Regions)
        {
            app.MapGet($"/{region}/widgets/availability", () =>
                    TypedResults.Ok(new Availability(region, UnitsIn(region))))
                .ExcludeFromDescription();
        }
    }

    private static int UnitsIn(string region) => region == "eu" ? 12 : 30;

    /// <summary>The request target as it arrived, path and query, still percent-encoded.</summary>
    private static string RawTarget(HttpContext context) =>
        context.Features.Get<IHttpRequestFeature>()?.RawTarget
            ?? context.Request.Path + context.Request.QueryString;

    /// <summary>
    /// Answers 401 unless the request carries a credential for <paramref name="scheme"/>. Anything
    /// non-empty passes, as with <see cref="RequireSampleToken"/>: the point is something for the
    /// try-it console's auth block to send.
    /// </summary>
    private static RouteHandlerBuilder RequireSampleCredential(
        this RouteHandlerBuilder endpoint, string scheme) =>
        endpoint
            .AddEndpointFilter(async (context, next) =>
            {
                IHeaderDictionary headers = context.HttpContext.Request.Headers;
                bool present = scheme == ApiKeyScheme
                    ? !string.IsNullOrEmpty(headers["X-Api-Key"])
                    : headers.Authorization.ToString() is { Length: > 6 } header
                        && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
                if (!present)
                {
                    if (scheme == BasicScheme)
                    {
                        // No realm prompt: a browser dialog over the docs page would be no help.
                        context.HttpContext.Response.Headers.WWWAuthenticate = "SampleBasic";
                    }

                    return TypedResults.Unauthorized();
                }

                return await next(context);
            })
            .WithMetadata(new SampleAuth(scheme))
            .Produces(StatusCodes.Status401Unauthorized);

    private static OpenApiSecurityScheme SchemeFor(string scheme) => scheme switch
    {
        ApiKeyScheme => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Api-Key",
            Description = "Any non-empty key is accepted by this sample.",
        },
        BasicScheme => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = "Any user name is accepted by this sample.",
        },
        _ => new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Any non-empty token is accepted by this sample.",
        },
    };
}
