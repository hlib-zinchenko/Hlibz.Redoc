using System.Net;
using System.Reflection;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// The try-it console: its script and stylesheet, embedded in this assembly, and the tag that
/// loads the script on a page.
/// </summary>
/// <remarks>
/// <para>
/// The script is identical on every page. What differs per page — where its spec is, and where
/// the app base is relative to it — goes in <c>data-</c> attributes on the script tag. The page is
/// built at startup anyway, and this keeps the script a static file with nothing inline.
/// </para>
/// <para>
/// The app base is a relative href (<c>../</c> per route segment), the same trick the picker's
/// links use. A spec without <c>servers</c> means <c>/</c> in OpenAPI, which is wrong under a
/// <c>PathBase</c> or behind a path-rewriting proxy. So the console sends to the app base instead,
/// and that base survives both.
/// </para>
/// </remarks>
internal static class ReDocTryIt
{
    public const string FileName = "redoc-tryit.js";

    private static readonly Lazy<string> ScriptText = new(() => Load("redoc-tryit.js"));
    private static readonly Lazy<string> CssText = new(() => Load("redoc-tryit.css"));

    public static string Script => ScriptText.Value;

    public static string Css => CssText.Value;

    /// <summary>The script tag a page carries when the console is on.</summary>
    /// <param name="specUrl">The spec URL the page hands ReDoc, as written in the init script.</param>
    /// <param name="route">The page's own route, which the app base is worked out from.</param>
    /// <param name="options">The console's settings.</param>
    public static string Tag(string specUrl, string route, ReDocTryItOptions options) =>
        $"<script type=\"module\" src=\"{FileName}\" data-hlibz-tryit"
        + $" data-spec-url=\"{WebUtility.HtmlEncode(specUrl)}\""
        + $" data-app-base=\"{ReDocRelativePath.Between(route, string.Empty)}\""
        + StorageAttribute(options.CredentialStorage)
        + LifetimeAttribute(options)
        + (options.SendCookies ? string.Empty : " data-send-cookies=\"false\"")
        + (options.SafeMethodsOnly ? " data-safe-methods-only=\"true\"" : string.Empty)
        + "></script>\n";

    // Whole seconds, at least one: the script compares it with Date.now(), and a lifetime set in
    // ticks would otherwise round to an instant expiry.
    private static string LifetimeAttribute(ReDocTryItOptions options) =>
        options.CredentialStorage == ReDocTryItCredentialStorage.Memory
        || options.CredentialLifetime is not { } lifetime
            ? string.Empty
            : FormattableString.Invariant(
                $" data-credential-lifetime=\"{Math.Max(1L, (long)lifetime.TotalSeconds)}\"");

    private static string StorageAttribute(ReDocTryItCredentialStorage storage) => storage switch
    {
        ReDocTryItCredentialStorage.Memory => string.Empty,
        ReDocTryItCredentialStorage.Session => " data-credential-storage=\"session\"",
        ReDocTryItCredentialStorage.Local => " data-credential-storage=\"local\"",
        _ => throw new ArgumentOutOfRangeException(
            nameof(storage), storage, "Not a ReDocTryItCredentialStorage value."),
    };

    private static string Load(string resourceName)
    {
        using Stream stream = typeof(ReDocTryIt).GetTypeInfo().Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded try-it file '{resourceName}' is missing from "
                + "Hlibz.Redoc.AspNetCore. This is a packaging bug, not something an app can "
                + "cause.");

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
