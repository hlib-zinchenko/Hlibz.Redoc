using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// The server half of the try-it console: whether a page loads it, what the page tells it, and
/// where it is served. The script's own behaviour — finding operations in ReDoc's DOM, building
/// requests — happens in a browser and was verified against <c>samples/Sample.Own</c>.
/// </summary>
public partial class ReDocTryItTests
{
    [Fact]
    public async Task Off_ByDefault_NoTagNoFileNoStyles()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync();

        string page = await app.GetStringAsync("/api-docs/locations/");
        string css = await app.GetStringAsync("/api-docs/locations/redoc-page.css");
        HttpResponseMessage script = await app.GetAsync("/api-docs/locations/redoc-tryit.js");

        Assert.DoesNotContain("redoc-tryit.js", page, StringComparison.Ordinal);
        Assert.DoesNotContain(".hlibz-tryit", css, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, script.StatusCode);
    }

    [Fact]
    public async Task On_ThePageLoadsTheScriptAndTellsItWhereItsSpecAndAppBaseAre()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());

        string page = await app.GetStringAsync("/api-docs/locations/");

        // The same spec URL ReDoc is handed, and the base two segments up: /api-docs/locations/
        // back to the app root, relative so a PathBase or a rewriting proxy changes nothing.
        Assert.Contains(
            "<script type=\"module\" src=\"redoc-tryit.js\" data-hlibz-tryit"
            + " data-spec-url=\"/openapi/locations.json\" data-app-base=\"../../\"></script>",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_ScriptAndStylesAreServedBesideThePage()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());

        HttpResponseMessage script = await app.GetAsync("/api-docs/taxes/redoc-tryit.js");
        string css = await app.GetStringAsync("/api-docs/taxes/redoc-page.css");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            ReDocTryIt.Script,
            await script.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(".hlibz-tryit-open", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_ThePageStillHasNothingInline()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());

        string page = await app.GetStringAsync("/api-docs/locations/");

        // The property the host is sold on: script-src 'self' with no nonce. Every <script> has a
        // src, so the console's per-page settings must travel as attributes.
        Assert.All(
            ScriptTag().Matches(page),
            tag => Assert.Contains(" src=\"", tag.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Settings_ReachThePageOnlyWhenTheyDifferFromTheDefault()
    {
        await using ReDocTestApp defaults = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());
        await using ReDocTestApp changed = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt =>
            {
                tryIt.CredentialStorage = ReDocTryItCredentialStorage.Local;
                tryIt.SendCookies = false;
            }));

        string plain = await defaults.GetStringAsync("/api-docs/locations/");
        string set = await changed.GetStringAsync("/api-docs/locations/");

        Assert.DoesNotContain("data-credential-storage", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("data-send-cookies", plain, StringComparison.Ordinal);
        Assert.Contains("data-credential-storage=\"local\"", set, StringComparison.Ordinal);
        Assert.Contains("data-send-cookies=\"false\"", set, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task SafeMethodsOnly_ReachesThePageAsTheScriptReadsIt(bool safeOnly, bool expect)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt => tryIt.SafeMethodsOnly = safeOnly));

        string page = await app.GetStringAsync("/api-docs/locations/");

        Assert.Equal(
            expect,
            page.Contains("data-safe-methods-only=\"true\"", StringComparison.Ordinal));
        // What the script compares against, so a rename on either side fails here.
        Assert.Contains(
            "getAttribute('data-safe-methods-only') === 'true'",
            ReDocTryIt.Script,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReDocTryItCredentialStorage.Memory, null)]
    [InlineData(ReDocTryItCredentialStorage.Session, "session")]
    [InlineData(ReDocTryItCredentialStorage.Local, "local")]
    public async Task CredentialStorage_ReachesThePageAsTheScriptReadsIt(
        ReDocTryItCredentialStorage storage, string? attribute)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt => tryIt.CredentialStorage = storage));

        string page = await app.GetStringAsync("/api-docs/locations/");

        if (attribute is null)
        {
            Assert.DoesNotContain("data-credential-storage", page, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(
                $"data-credential-storage=\"{attribute}\"", page, StringComparison.Ordinal);
            // The value the script compares against, so a rename on either side fails here.
            Assert.Contains($"config.storage === '{attribute}'", ReDocTryIt.Script,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CredentialStorage_AnUndefinedValueFailsAtStartup()
    {
        ReDocTryItOptions options = new() { CredentialStorage = (ReDocTryItCredentialStorage)42 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReDocTryIt.Tag("/openapi/x.json", "api-docs/x", options));
    }

    [Theory]
    [InlineData(ReDocTryItCredentialStorage.Memory, false)]
    [InlineData(ReDocTryItCredentialStorage.Session, true)]
    [InlineData(ReDocTryItCredentialStorage.Local, true)]
    public async Task StoredCredentials_TurnTheDefaultPolicyOn(
        ReDocTryItCredentialStorage storage, bool expectPolicy)
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt => tryIt.CredentialStorage = storage));

        HttpResponseMessage page = await app.GetAsync("/api-docs/locations/");

        if (expectPolicy)
        {
            Assert.Equal(
                ReDocDocumentsOptions.DefaultContentSecurityPolicy,
                Assert.Single(page.Headers.GetValues("Content-Security-Policy")));
        }
        else
        {
            Assert.False(page.Headers.Contains("Content-Security-Policy"));
        }
    }

    [Fact]
    public async Task StoredCredentials_APolicyTheCallerSetStillWins()
    {
        const string policy = "default-src 'self'; connect-src 'self' https://api.example.com";
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            configure: options =>
            {
                options.ContentSecurityPolicy = policy;
                options.EnableTryIt(
                    tryIt => tryIt.CredentialStorage = ReDocTryItCredentialStorage.Local);
            });

        HttpResponseMessage page = await app.GetAsync("/api-docs/locations/");

        Assert.Equal(policy, Assert.Single(page.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task StoredCredentials_ASpecOnAnotherOriginStaysReachable()
    {
        // The imposed policy would otherwise block ReDoc's own fetch of the spec: a page that
        // worked with no policy at all would go blank the moment storage was turned on.
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            documents:
            [
                new ReDocDocument("locations")
                {
                    SpecUrl = "https://specs.example.com:8443/locations.json",
                },
                new ReDocDocument("taxes") { SpecUrl = "//cdn.example.com/taxes.json" },
            ],
            configure: options => options.EnableTryIt(
                tryIt => tryIt.CredentialStorage = ReDocTryItCredentialStorage.Session));

        HttpResponseMessage page = await app.GetAsync("/api-docs/locations/");

        string policy = Assert.Single(page.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains(
            "connect-src 'self' cdn.example.com https://specs.example.com:8443;",
            policy,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/openapi/locations.json", null)]
    [InlineData("openapi.json", null)]
    [InlineData("../specs/locations.json", null)]
    [InlineData("https://specs.example.com/a.json", "https://specs.example.com")]
    [InlineData("http://localhost:5000/a.json", "http://localhost:5000")]
    [InlineData("//cdn.example.com/a.json", "cdn.example.com")]
    public void CrossOriginSource_OnlyForAnotherOrigin(string specUrl, string? expected) =>
        Assert.Equal(expected, ReDocSite.CrossOriginSource(specUrl));

    [Fact]
    public async Task CredentialLifetime_DefaultsToEightHoursOnceStored()
    {
        await using ReDocTestApp memory = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt());
        await using ReDocTestApp local = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(
                tryIt => tryIt.CredentialStorage = ReDocTryItCredentialStorage.Local));

        // Nothing is stored in memory mode, so there is nothing to expire.
        Assert.DoesNotContain(
            "data-credential-lifetime",
            await memory.GetStringAsync("/api-docs/locations/"),
            StringComparison.Ordinal);
        Assert.Contains(
            "data-credential-lifetime=\"28800\"",
            await local.GetStringAsync("/api-docs/locations/"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CredentialLifetime_ACallersValueReachesThePageAndNullMeansNoExpiry()
    {
        await using ReDocTestApp custom = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt =>
            {
                tryIt.CredentialStorage = ReDocTryItCredentialStorage.Session;
                tryIt.CredentialLifetime = TimeSpan.FromMinutes(30);
            }));
        await using ReDocTestApp forever = await ReDocTestApp.StartAsync(
            configure: options => options.EnableTryIt(tryIt =>
            {
                tryIt.CredentialStorage = ReDocTryItCredentialStorage.Local;
                tryIt.CredentialLifetime = null;
            }));

        Assert.Contains(
            "data-credential-lifetime=\"1800\"",
            await custom.GetStringAsync("/api-docs/locations/"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-credential-lifetime",
            await forever.GetStringAsync("/api-docs/locations/"),
            StringComparison.Ordinal);
        Assert.Contains("data-credential-lifetime", ReDocTryIt.Script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void CredentialLifetime_ZeroOrNegativeThrows(int seconds)
    {
        ReDocTryItOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => options.CredentialLifetime = TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public void EnableTryIt_Twice_KeepsTheEarlierSettings()
    {
        ReDocDocumentsOptions options = new();

        options.EnableTryIt(
            tryIt => tryIt.CredentialStorage = ReDocTryItCredentialStorage.Session).EnableTryIt();

        Assert.NotNull(options.TryIt);
        Assert.Equal(ReDocTryItCredentialStorage.Session, options.TryIt.CredentialStorage);
    }

    [Fact]
    public async Task AppBase_FollowsADeeperRoute()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            documents: [new ReDocDocument("locations", Route: "docs/internal/v1")],
            configure: options => options.EnableTryIt());

        string page = await app.GetStringAsync("/docs/internal/v1/");

        Assert.Contains("data-app-base=\"../../../\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecUrl_IsHtmlEncodedInTheAttribute()
    {
        await using ReDocTestApp app = await ReDocTestApp.StartAsync(
            documents:
            [
                new ReDocDocument(
                    "locations", SpecUrl: "/specs/locations.json?format=json&v=\"2\""),
            ],
            configure: options => options.EnableTryIt());

        string page = await app.GetStringAsync("/api-docs/locations/");

        Assert.Contains(
            "data-spec-url=\"/specs/locations.json?format=json&amp;v=&quot;2&quot;\"",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task MiddlewareForm_ServesTheScriptToo()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        await using WebApplication web = builder.Build();
        web.UseReDocDocuments(
            ReDocTestApp.DefaultDocuments, options => options.EnableTryIt());
        await web.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = new() { BaseAddress = new Uri(web.Urls.First()) };
        HttpResponseMessage response = await client.GetAsync(
            "/api-docs/locations/redoc-tryit.js", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void GatedDocument_ItsScriptCarriesThePolicyLikeEverythingElseOfIts()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddOpenApi("locations");
        builder.Services.AddOpenApi("taxes");
        WebApplication web = builder.Build();

        web.MapReDocDocuments(
            [
                new ReDocDocument("locations"),
                new ReDocDocument("taxes").RequireAuthorization("Employees"),
            ],
            options => options.EnableTryIt());

        Dictionary<string, string[]> policies = ((IEndpointRouteBuilder)web).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(
                endpoint => $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}",
                endpoint => endpoint.Metadata.OfType<IAuthorizeData>()
                    .Select(data => data.Policy ?? string.Empty)
                    .ToArray());

        Assert.Equal("Employees", Assert.Single(policies["/api-docs/taxes/redoc-tryit.js"]));
        Assert.Empty(policies["/api-docs/locations/redoc-tryit.js"]);
    }

    [Fact]
    public void EmbeddedFiles_ArePresent()
    {
        // A missing resource would only show as a 500 on the first request for it.
        Assert.Contains("data-hlibz-tryit", ReDocTryIt.Script, StringComparison.Ordinal);
        Assert.Contains("x-hideTryItPanel", ReDocTryIt.Script, StringComparison.Ordinal);
        Assert.Contains("dialog.hlibz-tryit", ReDocTryIt.Css, StringComparison.Ordinal);
    }

    [GeneratedRegex("<script[^>]*>")]
    private static partial Regex ScriptTag();
}
