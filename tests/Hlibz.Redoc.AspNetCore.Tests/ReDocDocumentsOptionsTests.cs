using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// What the options reject, and the startup check this host inherits from core.
/// </summary>
public class ReDocDocumentsOptionsTests
{
    [Fact]
    public void AdditionalConfigJson_ThatIsNotAnObject_ThrowsWhereItIsSet()
    {
        ReDocDocumentsOptions options = new();

        // A bad value here would otherwise reach the page and take the whole config down with it,
        // which is a blank page and one console error. Fail where the mistake is instead.
        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => options.AdditionalConfigJson = "[1, 2]");
        Assert.Equal(nameof(ReDocDocumentsOptions.AdditionalConfigJson), thrown.ParamName);
    }

    [Fact]
    public void AdditionalConfigJson_ThatIsNotJson_ThrowsWhereItIsSet()
    {
        ReDocDocumentsOptions options = new();

        Assert.Throws<ArgumentException>(() => options.AdditionalConfigJson = "{nope}");
    }

    [Fact]
    public void AdditionalConfigJson_IsMergedIntoTheServedConfig()
    {
        ReDocDocumentsOptions options = new()
        {
            AdditionalConfigJson = """{"showExtensions": true}""",
            HideDownloadButton = true,
        };

        string json = ReDocPage.ConfigJson(options);

        Assert.Contains("\"hideDownloadButton\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"showExtensions\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EnableTelemetry_IsOffByDefaultAndSaysSoInTheConfig()
    {
        Assert.Contains(
            "\"disableTelemetry\":true", ReDocPage.ConfigJson(new ReDocDocumentsOptions()),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"disableTelemetry\":false",
            ReDocPage.ConfigJson(new ReDocDocumentsOptions { EnableTelemetry = true }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MapReDocDocuments_WithAnUnregisteredDocumentName_ThrowsAtStartup()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddOpenApi("taxes");
        WebApplication app = builder.Build();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => app.MapReDocDocuments([new ReDocDocument("taxes"), new ReDocDocument("wigets")]));

        Assert.Contains("'wigets'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'taxes'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapReDocDocuments_WithNoDocuments_Throws()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapReDocDocuments([]));
    }

    [Fact]
    public void MapReDocDocuments_WithDuplicateRoutes_Throws()
    {
        WebApplication app = WebApplication.CreateSlimBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapReDocDocuments(
            [new ReDocDocument("taxes"), new ReDocDocument("fees", Route: "/api-docs/taxes/")]));
    }
}
