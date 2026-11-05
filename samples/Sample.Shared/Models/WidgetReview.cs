using System.ComponentModel;

namespace Sample.Shared.Models;

/// <summary>A review as a browser form posts it, rather than as JSON.</summary>
/// <remarks>
/// Properties rather than a positional record, on purpose: form binding requires every
/// constructor parameter, nullable or defaulted, and answers a bare 400 when one is missing, so a
/// review without tags could not be posted.
/// </remarks>
public sealed class WidgetReview
{
    [Description("Stars, from 1 to 5.")]
    public required int Rating { get; init; }

    [Description("What the reviewer had to say, if anything.")]
    public string? Comment { get; init; }

    [Description("Labels the reviewer picked. Repeat the field once per tag.")]
    public string[]? Tags { get; init; }
}
