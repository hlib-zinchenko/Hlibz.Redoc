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
    public async Task OperationWithAnOperationId_GetsAButtonToo()
    {
        // ReDoc anchors this one at /{tag}/getwidgetstock, not /{tag}/paths/..., so finding it
        // by path alone left it without a button.
        ILocator dialog = await OpenAtAsync("getwidgetstock");
        await Field(dialog, "id").FillAsync(KeyboardId);

        await SendAsync(dialog);

        await Expect(Status(dialog)).ToHaveTextAsync("200");
        await Expect(Response(dialog)).ToContainTextAsync(KeyboardId);
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
                    new() { HasTextRegex = new Regex("^" + Regex.Escape(label) + @"\b") }),
            })
            .Locator("input, select")
            .First;

    private static Task SendAsync(ILocator dialog) =>
        dialog.Locator(".hlibz-tryit-send").ClickAsync();

    private static ILocator Response(ILocator dialog) => dialog.Locator(".hlibz-tryit-response");

    private static ILocator Status(ILocator dialog) =>
        dialog.Locator(".hlibz-tryit-response .hlibz-tryit-badge");
}
