using System.Text.Json;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Options for <see cref="ReDocDocumentsBuilderExtensions.MapReDocDocuments"/> and
/// <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>. Everything up to
/// <see cref="DefaultColorMode"/> comes from <see cref="ReDocDocumentsOptionsBase"/> and is shared
/// with the adapters for Redoc.AspNetCore and Swashbuckle.
/// </summary>
/// <remarks>
/// Where an adapter hands you its host's <c>ReDocOptions</c> through a <c>ConfigureReDoc</c>
/// callback, this package has no host to configure — it <em>is</em> the host — so the page's own
/// settings are properties here.
/// </remarks>
public sealed class ReDocDocumentsOptions : ReDocDocumentsOptionsBase
{
    private string? additionalConfigJson;

    /// <summary>
    /// The color mode a first-time viewer sees. Defaults to <see cref="ReDocColorMode.System"/>.
    /// ReDoc keeps the viewer's own choice in <c>localStorage</c> and that always wins.
    /// </summary>
    public ReDocColorMode DefaultColorMode { get; set; } = ReDocColorMode.System;

    /// <summary>
    /// <c>href</c> for the page's <c>&lt;link rel="icon"&gt;</c>. No icon is declared when this is
    /// <see langword="null"/>, which leaves the browser asking for <c>/favicon.ico</c>.
    /// </summary>
    public string? Favicon { get; set; }

    /// <summary>
    /// CSS appended to the page's stylesheet, after this package's own rules. ReDoc 3 is themed
    /// through CSS custom properties, so overriding e.g. <c>--color-primary-base</c> under
    /// <c>html.dark</c> or <c>:root</c> is how a brand color is applied.
    /// </summary>
    public string? AdditionalCss { get; set; }

    /// <summary>
    /// Whether to hide ReDoc's "Download OpenAPI description" button. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool HideDownloadButton { get; set; }

    /// <summary>
    /// Whether to list required properties before optional ones in schemas. Defaults to
    /// <see langword="false"/>, which is ReDoc's own order.
    /// </summary>
    public bool RequiredPropsFirst { get; set; }

    /// <summary>
    /// Whether ReDoc may report anonymous usage telemetry. Defaults to <see langword="false"/>:
    /// a documentation page served from someone's own app shouldn't call out to a third party
    /// unless they asked for it.
    /// </summary>
    public bool EnableTelemetry { get; set; }

    /// <summary>
    /// Extra ReDoc configuration as a JSON object, merged into the generated config after this
    /// package's own settings, e.g. <c>"""{"showExtensions": true}"""</c>. An escape hatch for
    /// ReDoc options this package doesn't surface; it is parsed at startup and a value that isn't
    /// a JSON object throws there rather than producing a blank page.
    /// </summary>
    public string? AdditionalConfigJson
    {
        get => additionalConfigJson;
        set
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value))
            {
                Validate(value);
            }

            additionalConfigJson = value;
        }
    }

    private static void Validate(string json)
    {
        JsonValueKind kind;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            kind = document.RootElement.ValueKind;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"AdditionalConfigJson is not valid JSON: {exception.Message}",
                nameof(AdditionalConfigJson),
                exception);
        }

        if (kind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"AdditionalConfigJson must be a JSON object, e.g. {{\"showExtensions\": true}}, "
                + $"but it is a JSON {kind.ToString().ToLowerInvariant()}.",
                nameof(AdditionalConfigJson));
        }
    }
}
