using System.Text.RegularExpressions;

using Microsoft.Playwright;

using Sample.Shared.Models;

using Xunit;

using static Microsoft.Playwright.Assertions;

namespace Hlibz.Redoc.Extensions.BrowserTests;

/// <summary>
/// The half of the try-it console the C# suites can't reach: what the script does once ReDoc has
/// rendered. A smoke test of the paths a person checks by hand, not a catalogue of every branch —
/// each of these has broken once in a way only a browser showed.
/// </summary>
public sealed class TryItConsoleTests(TryItSite site) : IAsyncLifetime
{
    private static readonly string KeyboardId = Widget.Sample.First().Id.ToString();

    private IBrowserContext context = null!;
    private IPage page = null!;

    public async ValueTask InitializeAsync()
    {
        // A context per test: credentials live in the page's memory, so each test starts with none.
        context = await site.Browser.NewContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
    }

    public async ValueTask DisposeAsync() => await context.DisposeAsync();

    [Fact]
    public async Task PostWidget_IsRefusedWithoutATokenAndCreatedWithOne()
    {
        ILocator dialog = await OpenAsync("/widgets", "post");

        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("401");

        await Field(dialog, "Token").FillAsync("any-token");
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("201");
    }

    [Fact]
    public async Task PhotoUpload_SendsTheChosenFileAsMultipart()
    {
        ILocator dialog = await OpenAsync("/widgets/{id}/photos", "post");
        await Field(dialog, "Token").FillAsync("any-token");
        await Field(dialog, "id").FillAsync(KeyboardId);

        // The file is required, and saying so beats a 400 from the server.
        await SendAsync(dialog);
        await Expect(dialog.Locator(".hlibz-tryit-error"))
            .ToHaveTextAsync("Choose a file for: photo.");

        await dialog.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "front.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="),
        });
        await SendAsync(dialog);

        await Expect(Status(dialog)).ToHaveTextAsync("201");
        await Expect(Response(dialog)).ToContainTextAsync("front.png");
        await Expect(Response(dialog)).ToContainTextAsync("Front view, studio lighting");
    }

    [Fact]
    public async Task ReviewExamples_GoOutAsAUrlEncodedForm()
    {
        ILocator dialog = await OpenAsync("/widgets/{id}/reviews", "post");
        await Field(dialog, "Token").FillAsync("any-token");
        await Field(dialog, "id").FillAsync(KeyboardId);
        await Field(dialog, "Content type").SelectOptionAsync("application/x-www-form-urlencoded");

        // An array example is one line per item, which is how a form repeats a field.
        await Field(dialog, "Example").SelectOptionAsync("tagged");
        await Expect(dialog.Locator("textarea")).ToHaveValueAsync(
            new Regex("tags=clicky\ntags=sturdy"));
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("201");
        await Expect(Response(dialog)).ToContainTextAsync("sturdy");

        await Field(dialog, "Example").SelectOptionAsync("outOfRange");
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("400");
    }

    [Fact]
    public async Task Redirect_IsSaidRatherThanVouchedFor()
    {
        ILocator dialog = await OpenAsync("/widgets/moved", "get");

        await SendAsync(dialog);

        // fetch followed the redirect, so the 200 is the landing page's. Matching it against the
        // operation's documented 200 would have called it a success.
        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(dialog.Locator(".hlibz-tryit-undocumented"))
            .ToContainTextAsync("Redirected to " + site.Address + "/widgets/landing");
        await Expect(dialog.Locator(".hlibz-tryit-meaning")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Draft_SurvivesClosingAndReopening()
    {
        ILocator dialog = await OpenAsync("/widgets", "post");
        await dialog.Locator("textarea").FillAsync("{\"draft\": true}");

        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync();
        await OpenerFor("/widgets", "post").ClickAsync();

        await Expect(dialog.Locator("textarea")).ToHaveValueAsync("{\"draft\": true}");
    }

    [Fact]
    public async Task Search_SendsEachQueryParameterInItsDeclaredStyle()
    {
        // ReDoc anchors this one by its operationId, /{tag}/searchwidgets, not by its path, so
        // finding it by path alone once left it without a button.
        ILocator dialog = await OpenAtAsync("searchwidgets");

        // Every field starts from the spec's example; the enum and boolean are drop-downs.
        await Expect(Field(dialog, "status")).ToHaveValueAsync("Active");
        await Expect(Field(dialog, "shipping")).ToHaveValueAsync("true");
        await SendAsync(dialog);

        // pipeDelimited, then deepObject; the response carries the target as it arrived.
        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync(
            "/widgets/search?name=Keyboard&limit=10&status=Active&shipping=true"
            + "&tags=electronics%7Cfurniture&price%5Bmin%5D=100&price%5Bmax%5D=400");
        await Expect(Response(dialog)).ToContainTextAsync("Mechanical Keyboard");
        await Expect(Response(dialog)).Not.ToContainTextAsync("Standing Desk");
    }

    [Fact]
    public async Task Search_RefusesAnObjectParameterThatIsNotJson()
    {
        ILocator dialog = await OpenAtAsync("searchwidgets");
        await Field(dialog, "price").FillAsync("min=100");

        await SendAsync(dialog);

        await Expect(dialog.Locator(".hlibz-tryit-error"))
            .ToHaveTextAsync("price must be a JSON object, like {\"key\": \"value\"}.");
    }

    [Fact]
    public async Task Search_WarnsAboutValuesOutsideTheSchemaButStillSends()
    {
        ILocator dialog = await OpenAtAsync("searchwidgets");
        ILocator warnings = dialog.Locator(".hlibz-tryit-check:visible");
        await Expect(warnings).ToHaveCountAsync(0);

        await Field(dialog, "name").FillAsync("K");
        await Field(dialog, "limit").FillAsync("500");
        await Field(dialog, "tags").FillAsync("electronics,toys");

        await Expect(warnings).ToHaveTextAsync(
        [
            "The value should be at least 2 characters.",
            "The value should be at most 50.",
            "\"toys\" should be one of electronics, peripherals, furniture.",
        ]);

        // A warning, not a refusal: sending a bad value on purpose is half of what this is for.
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("200");
    }

    [Fact]
    public async Task ExtraHeader_IfNoneMatch_GetsA304()
    {
        ILocator dialog = await OpenAsync("/widgets/{id}", "get");
        await Field(dialog, "id").FillAsync(KeyboardId);
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("200");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add header" }).ClickAsync();
        await dialog.GetByLabel("Header name").FillAsync("bad name");
        await SendAsync(dialog);
        await Expect(dialog.Locator(".hlibz-tryit-error")).ToContainTextAsync(
            "\"bad name\" can't be sent as a header");

        await dialog.GetByLabel("Header name").FillAsync("If-None-Match");
        await dialog.GetByLabel("Header value").FillAsync($"\"{Guid.Parse(KeyboardId):N}-1\"");
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("304");
    }

    [Fact]
    public async Task Promotion_OffersEachKindAsAVariantWithItsDiscriminator()
    {
        ILocator dialog = await OpenAsync("/widgets/{id}/promotions", "post");
        await Field(dialog, "Token").FillAsync("any-token");
        await Field(dialog, "id").FillAsync(KeyboardId);

        ILocator body = dialog.Locator("textarea");
        await Expect(body).ToHaveValueAsync(new Regex("^\\{\\s*\"kind\": \"percentOff\""));

        await Field(dialog, "Variant").SelectOptionAsync("fixedPrice");
        await Expect(body).ToHaveValueAsync(new Regex("^\\{\\s*\"kind\": \"fixedPrice\""));
        await SendAsync(dialog);

        // The server reads the discriminator, and only from the first property: it's put there.
        await Expect(Status(dialog)).ToHaveTextAsync("201");
        await Expect(Response(dialog)).ToContainTextAsync("fixedPrice");
    }

    [Fact]
    public async Task DeprecatedOperation_SaysSoAndStillSends()
    {
        ILocator dialog = await OpenAsync("/widgets/by-name/{name}", "get");

        await Expect(dialog.Locator(".hlibz-tryit-deprecated")).ToContainTextAsync("Deprecated");
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync("Mechanical Keyboard");
    }

    [Fact]
    public async Task CopyAsFetch_IsCodeThatSendsTheSameRequest()
    {
        await context.GrantPermissionsAsync(
            ["clipboard-read", "clipboard-write"], new() { Origin = site.Address });
        ILocator dialog = await OpenAsync("/widgets", "post");
        await Field(dialog, "Token").FillAsync("any-token");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Copy as fetch" }).ClickAsync();
        string code = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");

        Assert.StartsWith($"const response = await fetch(\"{site.Address}/widgets\", {{", code);
        Assert.Contains("\"authorization\": \"Bearer any-token\"", code);

        // Run as written, it gets what the console's own Send got.
        int status = await page.EvaluateAsync<int>(
            "code => new (async function () {}).constructor(code + '\\nreturn response.status;')()",
            code);
        Assert.Equal(201, status);
    }

    [Fact]
    public async Task Compare_SendsItsPathParameterInMatrixStyle()
    {
        ILocator dialog = await OpenAsync("/widgets/compare/{ids}", "get");

        await SendAsync(dialog);

        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync(
            "/widgets/compare/;ids=" + string.Join(",", Widget.Sample.Select(w => w.Id)));
        await Expect(Response(dialog)).ToContainTextAsync("Standing Desk");
    }

    [Fact]
    public async Task Export_TakesAnApiKeyAndAnswersInTheAcceptedMediaType()
    {
        ILocator dialog = await OpenAsync("/widgets/export", "get");

        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("401");

        await Field(dialog, "Value").FillAsync("partner-key");
        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync("Mechanical Keyboard");

        await Field(dialog, "Accept").SelectOptionAsync("text/csv");
        await SendAsync(dialog);
        await Expect(Response(dialog)).ToContainTextAsync("id,name,price,status");
    }

    [Fact]
    public async Task Datasheet_SendsTheChosenFileAsTheWholeBody()
    {
        ILocator dialog = await OpenAsync("/widgets/{id}/datasheet", "put");
        await Field(dialog, "Username").FillAsync("supplier");
        await Field(dialog, "Password").FillAsync("secret");
        await Field(dialog, "id").FillAsync(KeyboardId);

        // There is no text to type for a PDF, so no editor either.
        await Expect(dialog.Locator("textarea")).Not.ToBeVisibleAsync();
        await SendAsync(dialog);
        await Expect(dialog.Locator(".hlibz-tryit-error"))
            .ToHaveTextAsync("Choose a file to send as the body.");

        byte[] pdf = new byte[4321];
        "%PDF-1.4"u8.CopyTo(pdf);
        await Field(dialog, "File").SetInputFilesAsync(new FilePayload
        {
            Name = "datasheet.pdf",
            MimeType = "application/pdf",
            Buffer = pdf,
        });
        await SendAsync(dialog);

        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync("application/pdf");
        await Expect(Response(dialog)).ToContainTextAsync("4321");

        // A wildcard type takes the file's own.
        await Field(dialog, "Content type").SelectOptionAsync("image/*");
        await Field(dialog, "File").SetInputFilesAsync(new FilePayload
        {
            Name = "scan.png",
            MimeType = "image/png",
            Buffer = [0x89, 0x50, 0x4E, 0x47],
        });
        await SendAsync(dialog);
        await Expect(Response(dialog)).ToContainTextAsync("image/png");
    }

    [Fact]
    public async Task Availability_GoesWhereTheServerVariableSays()
    {
        ILocator dialog = await OpenAsync("/widgets/availability", "get");

        await SendAsync(dialog);
        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(dialog.Locator(".hlibz-tryit-url"))
            .ToHaveTextAsync(site.Address + "/eu/widgets/availability");

        await Field(dialog, "{region}").SelectOptionAsync("us");
        await SendAsync(dialog);
        await Expect(dialog.Locator(".hlibz-tryit-url"))
            .ToHaveTextAsync(site.Address + "/us/widgets/availability");
        await Expect(Response(dialog)).ToContainTextAsync("30");

        // The other server has no variables, so the field goes with it.
        await Field(dialog, "Server").SelectOptionAsync("1");
        await Expect(Field(dialog, "{region}")).ToHaveCountAsync(0);
        await SendAsync(dialog);
        await Expect(dialog.Locator(".hlibz-tryit-url"))
            .ToHaveTextAsync(site.Address + "/widgets/availability");
    }

    /// <summary>Opens the console on an operation without an operationId.</summary>
    private Task<ILocator> OpenAsync(string path, string method) =>
        OpenAtAsync(Anchor(path, method));

    /// <summary>
    /// Opens the console on the operation whose anchor ends in <paramref name="anchor"/>. ReDoc
    /// renders only the sections near the viewport, so the page is sent to that anchor first; it
    /// starts with the slug of the operation's tag, which is read off the sidebar rather than
    /// assumed.
    /// </summary>
    private async Task<ILocator> OpenAtAsync(string anchor)
    {
        await page.GotoAsync(site.Address + "/api-docs/widgets/");
        string tag = (await page.Locator("a[href^='#/']:not([href='#/'])").First
            .GetAttributeAsync("href"))!;

        await page.EvaluateAsync("hash => { location.hash = hash; }", $"{tag}/{anchor}");
        await OpenerAt(anchor).ClickAsync();

        ILocator dialog = page.Locator("dialog.hlibz-tryit");
        await Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }

    private ILocator OpenerFor(string path, string method) => OpenerAt(Anchor(path, method));

    private ILocator OpenerAt(string anchor) =>
        page.Locator($"[data-section-id$='/{anchor}'] .hlibz-tryit-open");

    /// <summary>ReDoc's anchor for an operation that has no operationId.</summary>
    private static string Anchor(string path, string method) =>
        $"paths/{path.Replace("~", "~0").Replace("/", "~1")}/{method}";

    /// <summary>The input or select in the field whose label starts with <paramref name="label"/>.</summary>
    private static ILocator Field(ILocator dialog, string label) =>
        dialog.Locator(".hlibz-tryit-field")
            .Filter(new()
            {
                Has = dialog.Page.Locator(
                    ".hlibz-tryit-label",
                    // Not \b: a label ending in a symbol, like {area}, has no word boundary after it.
                    new() { HasTextRegex = new Regex("^" + Regex.Escape(label) + @"(?!\w)") }),
            })
            .Locator("input, select")
            .First;

    private static Task SendAsync(ILocator dialog) =>
        dialog.Locator(".hlibz-tryit-send").ClickAsync();

    private static ILocator Response(ILocator dialog) => dialog.Locator(".hlibz-tryit-response");

    private static ILocator Status(ILocator dialog) =>
        dialog.Locator(".hlibz-tryit-response .hlibz-tryit-badge");
}
