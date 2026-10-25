using System.ComponentModel;

namespace Sample.Shared.Models;

public sealed record ErrorResponse(
    [property: Description("Human-readable explanation of what went wrong.")] string Message,
    [property: Description("Correlation identifier for locating this request in logs.")] string TraceId);
