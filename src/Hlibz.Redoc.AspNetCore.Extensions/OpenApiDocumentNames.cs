using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Reads the set of registered OpenAPI document names out of a service provider.
/// </summary>
/// <remarks>
/// Both <c>Microsoft.AspNetCore.OpenApi</c> (net9.0+) and <c>Swashbuckle.AspNetCore.SwaggerGen</c>
/// register a <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c> whose
/// <c>GetDocumentNames()</c> returns exactly those names. The interface is internal in both, by
/// design: the <c>dotnet getdocument</c> build tool resolves it by type name, which is why the
/// namespace is fixed and why looking it up this way is the supported shape. Reflection is
/// therefore the only option even on net9.0+, so multi-targeting would not help.
/// </remarks>
internal static class OpenApiDocumentNames
{
    private const string ProviderTypeName =
        "Microsoft.Extensions.ApiDescriptions.IDocumentProvider";

    /// <summary>
    /// Returns every registered document name, compared case-insensitively because
    /// <c>MapOpenApi</c> lowercases the requested name before looking the document up, or
    /// <see langword="null"/> when no provider is registered at all — there is then nothing to
    /// validate against.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "A provider type or method that trimming removed resolves to null here, "
            + "which skips the check rather than failing it.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:UnrecognizedReflectionPattern",
        Justification = "Same: GetDocumentNames not being found leaves the names unvalidated.")]
    internal static HashSet<string>? TryResolve(IServiceProvider services)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        bool found = false;

        // An app referencing both Swashbuckle and Microsoft.AspNetCore.OpenApi has two distinct
        // IDocumentProvider types, each registered against its own, so union all of them.
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // This check must never be the thing that breaks an app's startup: a provider that
            // can't be loaded or resolved just doesn't contribute any names.
            try
            {
                Type? contract = assembly.GetType(ProviderTypeName, throwOnError: false);
                if (contract is null || !contract.IsInterface)
                {
                    continue;
                }

                object? provider = services.GetService(contract);
                MethodInfo? getNames = contract.GetMethod("GetDocumentNames", Type.EmptyTypes);
                if (provider is null || getNames is null)
                {
                    continue;
                }

                if (getNames.Invoke(provider, null) is not IEnumerable<string> resolved)
                {
                    continue;
                }

                found = true;
                foreach (string name in resolved)
                {
                    names.Add(name);
                }
            }
            catch (Exception)
            {
                // Unloadable assembly, broken registration, throwing provider: skip it.
            }
        }

        return found ? names : null;
    }
}
