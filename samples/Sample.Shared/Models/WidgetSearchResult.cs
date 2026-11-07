using System.ComponentModel;

namespace Sample.Shared.Models;

/// <summary>The widgets a search or comparison found, and the request as it arrived.</summary>
public sealed record WidgetSearchResult(
    [property: Description("The request target as the server received it, still percent-encoded, so the parameters' wire format can be read off it.")]
        string Received,
    [property: Description("The widgets that matched.")] IReadOnlyCollection<Widget> Widgets);
