using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// The picker's script, asserted here rather than in an adapter suite because it is the same
/// script on every host.
/// </summary>
public class ReDocDocumentPickerTests
{
    [Fact]
    public void Groups_PutEachDocumentInAnOptgroup()
    {
        string script = BuildFor(
            new ReDocDocument("v1", Group: "Payments"),
            new ReDocDocument("v2", Group: "Payments"),
            new ReDocDocument("users", Group: "Identity"));

        Assert.Contains("createElement('optgroup')", script, StringComparison.Ordinal);
        Assert.Contains("addOption(groupFor(select, doc), doc)", script, StringComparison.Ordinal);
        Assert.Contains("\"group\":\"Payments\"", script, StringComparison.Ordinal);
        Assert.Contains("\"group\":\"Identity\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Groups_LeaveAnUngroupedDocumentDirectlyInTheList()
    {
        string script = BuildFor(
            new ReDocDocument("overview"),
            new ReDocDocument("v1", Group: "Payments"));

        // groupFor returns the select itself for an entry with no group, so the two can be mixed
        // and the configured order is what you see.
        Assert.Contains("if (!doc.group) { return select; }", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"label\":\"overview\",\"href\":\"./\",\"current\":true,\"group\"",
            script, StringComparison.Ordinal);
    }

    [Fact]
    public void Groups_WhenNoneAreUsed_LeaveTheScriptExactlyAsItWas()
    {
        string script = BuildFor(new ReDocDocument("taxes"), new ReDocDocument("locations"));

        // Core's output is inlined verbatim by both adapters, so an opt-in feature has to be
        // invisible until it is opted into — not merely harmless. The placeholders that carry the
        // grouping code collapse to the old text, blank line included.
        Assert.DoesNotContain("group", script, StringComparison.Ordinal);
        Assert.Contains(
            """
                select.appendChild(option);
                return option;
              }

              function render() {
            """,
            script,
            StringComparison.Ordinal);
        Assert.Contains("var option = addOption(select, doc);", script, StringComparison.Ordinal);
    }

    /// <summary>The script a site of these documents serves on its first document's page.</summary>
    private static string BuildFor(params ReDocDocument[] documents)
    {
        ReDocDocumentPage[] pages =
        [
            .. documents.Select(document =>
                new ReDocDocumentPage(document, $"api-docs/{document.Name}")),
        ];

        return ReDocDocumentPicker.BuildScript(pages, [], pages[0]);
    }
}
