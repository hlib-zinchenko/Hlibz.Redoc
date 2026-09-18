using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hlibz.Redoc.AspNetCore.Extensions.Tests;

public class ReDocDocumentsBuilderExtensionsTests
{
    [Fact]
    public void UseReDocDocuments_WithNoDocuments_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments([]));
    }

    [Fact]
    public void UseReDocDocuments_WithDuplicateNames_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("taxes"), new ReDocDocument("Taxes")]));
    }

    [Fact]
    public void UseReDocDocuments_WithSlashInName_Throws()
    {
        ApplicationBuilder app = CreateApp();

        Assert.Throws<ArgumentException>(() => app.UseReDocDocuments(
            [new ReDocDocument("v1/taxes")]));
    }

    [Theory]
    [InlineData("/api-docs")]
    [InlineData("/api-docs/")]
    public async Task UseReDocDocuments_RoutePrefix_RedirectsToFirstDocument(string path)
    {
        await using WebApplication app = WebApplication.CreateSlimBuilder().Build();
        app.UseReDocDocuments([new ReDocDocument("locations"), new ReDocDocument("taxes")]);
        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();
        DefaultHttpContext context = new() { RequestServices = app.Services };
        context.Request.Method = HttpMethods.Get;
        context.Request.PathBase = "/base";
        context.Request.Path = path;

        await pipeline(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/base/api-docs/locations/", context.Response.Headers.Location.ToString());
    }

    private static ApplicationBuilder CreateApp()
    {
        return new ApplicationBuilder(new ServiceCollection().AddOptions().BuildServiceProvider());
    }
}
