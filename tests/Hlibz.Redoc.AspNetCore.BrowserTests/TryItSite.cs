using Hlibz.Redoc.Extensions.BrowserTests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Sample.Shared;

using Xunit;

[assembly: AssemblyFixture(typeof(TryItSite))]

namespace Hlibz.Redoc.Extensions.BrowserTests;

/// <summary>
/// One app and one browser for the whole run: the samples' widget catalog behind the try-it
/// console, on a real server, and a Chromium to drive it.
/// </summary>
/// <remarks>
/// The browser is the system Chrome when there is one — GitHub's Ubuntu runners have it, and so
/// does most every machine this is run on — so nothing is downloaded. Otherwise Playwright's own
/// Chromium is installed, through the package's own entry point rather than its PowerShell script,
/// so neither path needs anything beyond the .NET SDK.
/// </remarks>
public sealed class TryItSite : IAsyncLifetime
{
    private WebApplication? app;
    private IPlaywright? playwright;

    public string Address { get; private set; } = string.Empty;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSampleDocuments();

        app = builder.Build();
        app.MapSampleEndpoints();

        // Not in the samples: a documented operation that answers with a redirect, which fetch
        // follows without a word. Tagged into the widget catalog so it renders on that page.
        app.MapGet("/widgets/moved", () => TypedResults.Redirect("/widgets/landing"))
            .Produces(StatusCodes.Status200OK)
            .WithGroupName("widgets");
        app.MapGet("/widgets/landing", () => TypedResults.Text("Sign in first."))
            .ExcludeFromDescription();

        // Nor this: an operation with an operationId, which ReDoc anchors by that id rather than
        // by its path, so the script has to find it a second way.
        app.MapGet("/widgets/{id:guid}/stock", (Guid id) => TypedResults.Ok(new { id, inStock = 3 }))
            .WithName("GetWidgetStock")
            .WithGroupName("widgets");

        app.MapReDocDocuments(
            [new ReDocDocument("widgets", "Widget Catalog")],
            options =>
            {
                // Each page serves its own spec, so the app needs no MapOpenApi().
                options.ServeSpecs = true;
                options.EnableTryIt();
            });

        await app.StartAsync();
        Address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        playwright = await Playwright.CreateAsync();
        try
        {
            Browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome" });
        }
        catch (PlaywrightException)
        {
            int exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"No Chrome here, and installing Playwright's Chromium failed ({exitCode}).");
            }

            Browser = await playwright.Chromium.LaunchAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        playwright?.Dispose();
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }
}
