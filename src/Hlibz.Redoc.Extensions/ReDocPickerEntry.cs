using System.Text.Json.Serialization;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// One entry of the document picker, as the picker's script reads it.
/// </summary>
/// <param name="Label">Text shown in the dropdown.</param>
/// <param name="Href">Where selecting it navigates, relative to the current page.</param>
/// <param name="Current">Whether this entry is the page being viewed.</param>
internal sealed record ReDocPickerEntry(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("current")] bool Current = false);

/// <summary>
/// Source-generated serialization for <see cref="ReDocPickerEntry"/>, so the package stays
/// trim- and AOT-clean: the reflection-based <c>JsonSerializer.Serialize</c> overloads are not.
/// </summary>
[JsonSerializable(typeof(ReDocPickerEntry[]))]
internal sealed partial class ReDocPickerJson : JsonSerializerContext;
