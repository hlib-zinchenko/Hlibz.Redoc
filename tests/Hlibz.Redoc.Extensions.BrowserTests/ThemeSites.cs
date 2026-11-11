using Hlibz.Redoc.Extensions.ThemeTests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Sample.Shared;

using Xunit;

[assembly: AssemblyFixture(typeof(ThemeSites))]

namespace Hlibz.Redoc.Extensions.ThemeTests;

/// <summary>The two ReDoc 2 hosts an adapter exists for.</summary>
public enum ReDocHost
{
    /// <summary>Redoc.AspNetCore, which bundles redoc 2.0.0.</summary>
    RedocAspNetCore,

    /// <summary>Swashbuckle.AspNetCore.ReDoc, which bundles redoc 2.5.4.</summary>
    Swashbuckle,
}

/// <summary>
/// The samples' documents on both hosts, each on a real server, and one Chromium for the run.
/// </summary>
/// <remarks>
/// Both apps run in this one process — the adapters are referenced under extern aliases — so a
/// test can probe the same page on each and compare the two. The browser is the system Chrome
/// when there is one, else Playwright's own Chromium, installed through the package's entry point,
/// exactly as the try-it console's suite does it.
/// </remarks>
public sealed class ThemeSites : IAsyncLifetime
{
    /// <summary>The document whose page carries the Light/System/Dark selector.</summary>
    public const string SelectorDocument = "manufacturers";

    private readonly Dictionary<ReDocHost, WebApplication> apps = [];
    private readonly Dictionary<ReDocHost, string> addresses = [];
    private IPlaywright? playwright;

    public IBrowser Browser { get; private set; } = null!;

    public string AddressOf(ReDocHost host) => addresses[host];

    public async ValueTask InitializeAsync()
    {
        await StartAsync(ReDocHost.RedocAspNetCore, RedocAspNetCoreSite.Map);
        await StartAsync(ReDocHost.Swashbuckle, SwashbuckleSite.Map);

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
        foreach (WebApplication app in apps.Values)
        {
            await app.DisposeAsync();
        }
    }

    private async Task StartAsync(ReDocHost host, Action<IApplicationBuilder> mapPages)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSampleDocuments();

        WebApplication app = builder.Build();
        app.MapOpenApi();
        mapPages(app);
        app.MapSampleEndpoints();

        await app.StartAsync();
        apps[host] = app;
        addresses[host] = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }
}
