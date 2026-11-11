using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// <c>SpecCacheDuration</c>: a served spec generated once per period and revalidated by ETag —
/// and never for a document behind authorization, however it was gated.
/// </summary>
public class ReDocSpecCacheTests
{
    private const string LocationsSpec = "/api-docs/locations/openapi.json";
    private const string TaxesSpec = "/api-docs/taxes/openapi.json";

    [Fact]
    public async Task CachedSpec_IsGeneratedOnce_AndRevalidates()
    {
        Generations generations = new();
        await using ReDocTestApp app = await StartAsync(
            generations, options => options.SpecCacheDuration = TimeSpan.FromMinutes(5));

        HttpResponseMessage first = await app.GetAsync(LocationsSpec);
        HttpResponseMessage second = await app.GetAsync(LocationsSpec);
        HttpResponseMessage revalidated = await app.GetAsync(
            LocationsSpec, ("If-None-Match", first.Headers.ETag!.ToString()));

        Assert.Equal(1, generations.Of("locations"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.True(first.Headers.CacheControl?.Private);
        Assert.True(first.Headers.CacheControl?.NoCache);
        Assert.False(first.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotModified, revalidated.StatusCode);
    }

    [Fact]
    public async Task CachedSpec_IsGeneratedAgain_OnceItExpires()
    {
        Generations generations = new();
        ManualTime time = new();
        await using ReDocTestApp app = await StartAsync(
            generations,
            options => options.SpecCacheDuration = TimeSpan.FromMinutes(5),
            time: time);

        await app.GetAsync(LocationsSpec);
        time.Advance(TimeSpan.FromMinutes(4));
        await app.GetAsync(LocationsSpec);
        Assert.Equal(1, generations.Of("locations"));

        time.Advance(TimeSpan.FromMinutes(2));
        await app.GetAsync(LocationsSpec);
        Assert.Equal(2, generations.Of("locations"));
    }

    [Fact]
    public async Task WithoutTheOption_TheSpecIsGeneratedPerRequest()
    {
        Generations generations = new();
        await using ReDocTestApp app = await StartAsync(generations, _ => { });

        HttpResponseMessage response = await app.GetAsync(LocationsSpec);
        await app.GetAsync(LocationsSpec);

        Assert.Equal(2, generations.Of("locations"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task GatedDocument_IsNeverCached()
    {
        Generations generations = new();
        await using ReDocTestApp app = await StartAsync(
            generations,
            options => options.SpecCacheDuration = TimeSpan.FromMinutes(5),
            documents:
            [
                new ReDocDocument("locations"),
                new ReDocDocument("taxes").RequireAuthorization(OpenPolicy),
            ]);

        HttpResponseMessage gated = await app.GetAsync(TaxesSpec);
        await app.GetAsync(TaxesSpec);
        await app.GetAsync(LocationsSpec);
        await app.GetAsync(LocationsSpec);

        Assert.Equal(HttpStatusCode.OK, gated.StatusCode);
        Assert.True(gated.Headers.CacheControl?.NoStore);
        Assert.Equal(2, generations.Of("taxes"));

        // The public document beside it is still cached.
        Assert.Equal(1, generations.Of("locations"));
    }

    [Fact]
    public async Task DocumentGatedThroughTheBuilder_IsNeverCached()
    {
        Generations generations = new();
        await using ReDocTestApp app = await StartAsync(
            generations,
            options =>
            {
                options.ServeSpecs = true;
                options.SpecCacheDuration = TimeSpan.FromMinutes(5);
            },
            configureEndpoints: mapped =>
                mapped.ForDocument("taxes").RequireAuthorization(OpenPolicy));

        HttpResponseMessage response = await app.GetAsync(TaxesSpec);
        await app.GetAsync(TaxesSpec);

        // Invisible when the site was built, so caught per request by the endpoint's metadata.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(2, generations.Of("taxes"));
    }

    [Fact]
    public async Task WithNoServedSpec_Throws()
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReDocTestApp.StartAsync(
                configure: options => options.SpecCacheDuration = TimeSpan.FromMinutes(5)));

        Assert.Contains("SpecCacheDuration", error.Message, StringComparison.Ordinal);
        Assert.Contains("ServeSpecs", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveDuration_Throws(int seconds)
    {
        ReDocDocumentsOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => options.SpecCacheDuration = TimeSpan.FromSeconds(seconds));
    }

    // A policy that lets everyone through without any authentication scheme, so a gated request
    // answers 200 and the headers it carries can be read.
    private const string OpenPolicy = "Open";

    private static Task<ReDocTestApp> StartAsync(
        Generations generations,
        Action<ReDocDocumentsOptions> configure,
        IEnumerable<ReDocDocument>? documents = null,
        ManualTime? time = null,
        Action<ReDocDocumentsConventionBuilder>? configureEndpoints = null) =>
        ReDocTestApp.StartAsync(
            documents ?? [new ReDocDocument("locations"), new ReDocDocument("taxes")],
            options =>
            {
                options.ServeSpecs = true;
                configure(options);
            },
            configureServices: builder =>
            {
                foreach (string name in new[] { "locations", "taxes" })
                {
                    builder.Services.AddOpenApi(name, openApi => openApi.AddDocumentTransformer(
                        (_, _, _) =>
                        {
                            generations.Count(name);
                            return Task.CompletedTask;
                        }));
                }

                builder.Services.AddAuthorization(authorization => authorization.AddPolicy(
                    OpenPolicy, policy => policy.RequireAssertion(_ => true)));
                if (time is not null)
                {
                    builder.Services.AddSingleton<TimeProvider>(time);
                }
            },
            configureApp: web => web.UseAuthorization(),
            configureEndpoints: configureEndpoints);

    /// <summary>How many times each document has been generated.</summary>
    private sealed class Generations
    {
        private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);

        public void Count(string name)
        {
            lock (counts)
            {
                counts[name] = Of(name) + 1;
            }
        }

        public int Of(string name)
        {
            lock (counts)
            {
                return counts.GetValueOrDefault(name);
            }
        }
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
