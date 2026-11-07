using System.ComponentModel;

namespace Sample.Shared.Models;

/// <summary>What the sample did with an uploaded datasheet: nothing but measure it.</summary>
public sealed record DatasheetReceipt(
    [property: Description("The widget the datasheet was uploaded for.")] Guid WidgetId,
    [property: Description("The media type the client sent the body as.")] string ContentType,
    [property: Description("Size of the body, in bytes.")] long Length);
