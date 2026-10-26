using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// An app serving the documents under test, over a real server on an ephemeral port.
/// </summary>
/// <remarks>
/// A started host rather than a hand-built pipeline, because endpoint routing needs one: a
/// <c>RequestDelegate</c> built straight off <see cref="WebApplication"/> matches nothing, which
/// is the difference between testing <c>MapReDocDocuments</c> and testing the adapters'
/// middleware. Kestrel on port 0 also means the assertions see the response a browser would,
/// headers included.
/// </remarks>
internal sealed class ReDocTestApp : IAsyncDisposable
{
    private readonly WebApplication app;

    private ReDocTestApp(WebApplication app, HttpClient client)
    {
        this.app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<ReDocTestApp> StartAsync(
        IEnumerable<ReDocDocument>? documents = null,
        Action<ReDocDocumentsOptions>? configure = null,
        string pathBase = "",
        Action<WebApplicationBuilder>? configureServices = null,
        Action<WebApplication>? configureApp = null,
        Action<ReDocDocumentsConventionBuilder>? configureEndpoints = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        configureServices?.Invoke(builder);

        WebApplication app = builder.Build();
        if (pathBase.Length > 0)
        {
            app.UsePathBase(pathBase);
        }

        // Before the pages are mapped, which is where an app's own middleware would sit.
        configureApp?.Invoke(app);

        // Mapped first, on its own line: a null-conditional short-circuits its argument, so
        // `configureEndpoints?.Invoke(app.MapReDocDocuments(...))` maps nothing at all when no
        // conventions are wanted — which is most of this suite.
        ReDocDocumentsConventionBuilder mapped =
            app.MapReDocDocuments(documents ?? DefaultDocuments, configure);
        configureEndpoints?.Invoke(mapped);

        await app.StartAsync();

        string address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        // Redirects are the thing under test in several places, so never follow them. No
        // automatic decompression either: whether the bundle arrives gzipped is an assertion.
        HttpClient client = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
        })
        {
            BaseAddress = new Uri(address),
        };

        return new ReDocTestApp(app, client);
    }

    public static IEnumerable<ReDocDocument> DefaultDocuments =>
        [new ReDocDocument("locations"), new ReDocDocument("taxes")];

    /// <summary>A GET, with optional request headers, honouring the test's cancellation token.</summary>
    public Task<HttpResponseMessage> GetAsync(
        string path, params (string Name, string Value)[] headers)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        foreach ((string name, string value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A GET that must succeed, as text.</summary>
    public async Task<string> GetStringAsync(
        string path, params (string Name, string Value)[] headers)
    {
        HttpResponseMessage response = await GetAsync(path, headers);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
