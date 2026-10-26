using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Xunit;

namespace Hlibz.Redoc.Extensions.Conformance;

/// <summary>
/// The startup check that every document name matches a registered OpenAPI document. These run
/// against the real providers on purpose: the check resolves their internal
/// <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c> by type name, so the type and
/// method names are the part worth protecting.
/// </summary>
public class OpenApiDocumentNameConformanceTests
{
    [Fact]
    public void WithMatchingNativeDocument_DoesNotThrow()
    {
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        app.UseReDocDocuments([new ReDocDocument("widgets")]);
    }

    [Fact]
    public void WithMisspelledNativeDocument_ThrowsNamingThePageAndTheRegisteredDocuments()
    {
        WebApplication app = CreateApp(services =>
        {
            services.AddOpenApi("widgets");
            services.AddOpenApi("manufacturers");
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => app.UseReDocDocuments([new ReDocDocument("wigets")]));

        Assert.StartsWith("UseReDocDocuments was given", error.Message);
        Assert.Contains("'wigets'", error.Message);
        Assert.Contains("'/api-docs/wigets/'", error.Message);
        Assert.Contains("'/openapi/wigets.json'", error.Message);
        Assert.Contains("Registered documents: 'manufacturers', 'widgets'.", error.Message);
        Assert.Contains("ValidateDocumentNames = false", error.Message);
    }

    [Fact]
    public void WithDifferentlyCasedName_DoesNotThrow()
    {
        // MapOpenApi lowercases the requested name before looking the document up, so serving is
        // case-insensitive and so is this check.
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        app.UseReDocDocuments([new ReDocDocument("WIDGETS")]);
    }

    [Fact]
    public void WithMisspelledSwashbuckleDocument_Throws()
    {
        WebApplication app = CreateApp(services =>
        {
            // Swashbuckle's provider needs the API explorer, which every Swashbuckle app has
            // either through AddEndpointsApiExplorer() or through AddControllers().
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(
                swagger => swagger.SwaggerDoc("v1", new OpenApiInfo()));
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => app.UseReDocDocuments([new ReDocDocument("v2")]));

        Assert.Contains("'v2'", error.Message);
        Assert.Contains("Registered documents: 'v1'.", error.Message);
    }

    [Fact]
    public void WithBothProvidersRegistered_UnionsTheirDocuments()
    {
        // Each package declares its own IDocumentProvider type, so both have to be consulted.
        WebApplication app = CreateApp(services =>
        {
            services.AddOpenApi("widgets");
            services.AddSwaggerGen(
                swagger => swagger.SwaggerDoc("v1", new OpenApiInfo()));
        });

        app.UseReDocDocuments([new ReDocDocument("widgets"), new ReDocDocument("v1")]);
    }

    [Fact]
    public void WithNoProviderRegistered_DoesNotThrow()
    {
        WebApplication app = CreateApp(_ => { });

        app.UseReDocDocuments([new ReDocDocument("anything")]);
    }

    [Fact]
    public void WithExplicitSpecUrl_DoesNotThrow()
    {
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        app.UseReDocDocuments([new ReDocDocument("legacy", SpecUrl: "/specs/legacy.yaml")]);
    }

    [Fact]
    public void WithSpecUrlTemplateWithoutThePlaceholder_DoesNotThrow()
    {
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        app.UseReDocDocuments(
            [new ReDocDocument("legacy")],
            docs => docs.SpecUrlTemplate = "/openapi/v1.json");
    }

    [Fact]
    public void WhenValidationIsDisabled_DoesNotThrow()
    {
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        app.UseReDocDocuments(
            [new ReDocDocument("wigets")],
            docs => docs.ValidateDocumentNames = false);
    }

    [Fact]
    public void WithSeveralMisspelledDocuments_ReportsThemAllAtOnce()
    {
        WebApplication app = CreateApp(services => services.AddOpenApi("widgets"));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => app.UseReDocDocuments(
                [new ReDocDocument("wigets"), new ReDocDocument("fees")]));

        Assert.Contains("'wigets'", error.Message);
        Assert.Contains("'fees'", error.Message);
    }

    private static WebApplication CreateApp(Action<IServiceCollection> configureServices)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        configureServices(builder.Services);
        return builder.Build();
    }
}
