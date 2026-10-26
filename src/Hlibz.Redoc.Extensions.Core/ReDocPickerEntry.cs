using System.Text.Json.Serialization;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// One entry of the document picker, as the picker's script reads it.
/// </summary>
/// <param name="Label">Text shown in the dropdown.</param>
/// <param name="Href">Where selecting it navigates, relative to the current page.</param>
/// <param name="Current">Whether this entry is the page being viewed.</param>
/// <param name="Group">
/// The <c>&lt;optgroup&gt;</c> this entry belongs in, or <see langword="null"/> to sit directly in
/// the list. Omitted from the JSON when null, so a site that groups nothing serves exactly the
/// bytes it served before groups existed.
/// </param>
internal sealed record ReDocPickerEntry(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("current")] bool Current = false,
    [property: JsonPropertyName("group")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Group = null);

/// <summary>
/// Source-generated serialization for <see cref="ReDocPickerEntry"/>, so the package stays
/// trim- and AOT-clean: the reflection-based <c>JsonSerializer.Serialize</c> overloads are not.
/// </summary>
[JsonSerializable(typeof(ReDocPickerEntry[]))]
internal sealed partial class ReDocPickerJson : JsonSerializerContext;
