using System.ComponentModel;

namespace Sample.Shared.Models;

/// <summary>What the sample did with an uploaded photo: nothing but measure it.</summary>
public sealed record PhotoReceipt(
    [property: Description("The widget the photo was uploaded for.")] Guid WidgetId,
    [property: Description("The file name the client sent.")] string FileName,
    [property: Description("The media type the client declared for the file.")] string ContentType,
    [property: Description("Size of the uploaded file, in bytes.")] long Length,
    [property: Description("The caption sent alongside the file, if any.")] string? Caption);
