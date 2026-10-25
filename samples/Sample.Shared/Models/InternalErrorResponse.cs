using System.ComponentModel;

namespace Sample.Shared.Models;

public sealed record InternalErrorResponse(
    [property: Description("Human-readable, non-sensitive summary of the failure.")] string Message,
    [property: Description("Correlation identifier for locating this request in logs.")] string TraceId,
    [property: Description("When the error occurred, in UTC.")] DateTimeOffset OccurredAt);
