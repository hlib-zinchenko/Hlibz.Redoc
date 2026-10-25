using System.ComponentModel;

namespace Sample.Shared.Models;

public sealed record ValidationErrorResponse(
    [property: Description("Human-readable summary of the validation failure.")] string Message,
    [property: Description("Validation errors keyed by the name of the field that failed.")]
        IReadOnlyDictionary<string, string[]> Errors);
