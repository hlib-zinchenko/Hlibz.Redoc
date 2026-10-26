using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Generates one document's OpenAPI JSON on demand, so a page can serve its own spec from its own
/// route instead of pointing at an endpoint somebody else mapped.
/// </summary>
/// <remarks>
/// <para>
/// This is the other half of putting a page behind a policy. A policy on a page's endpoints is
/// worth little while the spec that page reads sits on an open endpoint of its own, and nothing
/// here can reach that endpoint: <c>MapOpenApi</c> and <c>MapSwagger</c> map <em>one</em> route for
/// every document, so there is no per-document thing to attach a policy to. A spec served from
/// inside the page's own route is in that document's endpoint group, where one
/// <c>RequireAuthorization</c> covers the page and the spec together.
/// </para>
/// <para>
/// The hook is <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c>, the same one
/// <see cref="OpenApiDocumentNames"/> already resolves for the startup name check. It is internal
/// in every implementation by design — the <c>dotnet getdocument</c> build tool resolves it by
/// type name as well — which is both why reflection is the only way in and why one lookup covers
/// <c>Microsoft.AspNetCore.OpenApi</c>, Swashbuckle and NSwag alike.
/// </para>
/// <para>
/// The provider is resolved per request rather than held, because an implementation may be scoped
/// and because a spec is generated from the app's current api descriptions. Which provider,
/// though, is settled at startup: an app referencing two generators has two distinct
/// <c>IDocumentProvider</c> types, and only one of them knows this document.
/// </para>
/// </remarks>
internal sealed class OpenApiSpecSource
{
    private const string ProviderTypeName =
        "Microsoft.Extensions.ApiDescriptions.IDocumentProvider";

    private readonly Type contract;
    private readonly MethodInfo generate;
    private readonly string documentName;

    private OpenApiSpecSource(Type contract, MethodInfo generate, string documentName)
    {
        this.contract = contract;
        this.generate = generate;
        this.documentName = documentName;
    }

    /// <summary>
    /// The source for one document, or <see langword="null"/> when no registered provider offers a
    /// document of that name — in which case the caller has to fail at startup rather than serve
    /// a page whose spec request would throw.
    /// </summary>
    /// <remarks>
    /// The name is matched case-insensitively, the same way it is matched everywhere else, but the
    /// <em>provider's</em> spelling is what gets passed back to it: Swashbuckle looks its documents
    /// up in an ordinal dictionary, so "V1" would not find "v1" there.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "A provider type trimming removed resolves to null here, which makes the "
            + "caller throw at startup with the message that says so.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:UnrecognizedReflectionPattern",
        Justification = "Same: a GenerateAsync that can't be found is a startup failure, not a "
            + "page that serves a broken spec.")]
    internal static OpenApiSpecSource? TryResolve(IServiceProvider services, string documentName)
    {
        // A scope of our own: an implementation registered scoped can't be resolved from the root
        // provider, and at startup there is no request scope to borrow.
        using IServiceScope scope = services.CreateScope();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Nothing in here may break startup on its own account — a provider that can't be
            // loaded simply isn't the one serving this document.
            try
            {
                Type? contract = assembly.GetType(ProviderTypeName, throwOnError: false);
                if (contract is null || !contract.IsInterface)
                {
                    continue;
                }

                object? provider = scope.ServiceProvider.GetService(contract);
                MethodInfo? getNames = contract.GetMethod("GetDocumentNames", Type.EmptyTypes);
                MethodInfo? generate =
                    contract.GetMethod("GenerateAsync", [typeof(string), typeof(TextWriter)]);
                if (provider is null || getNames is null || generate is null)
                {
                    continue;
                }

                if (getNames.Invoke(provider, null) is not IEnumerable<string> names)
                {
                    continue;
                }

                foreach (string name in names)
                {
                    if (string.Equals(name, documentName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new OpenApiSpecSource(contract, generate, name);
                    }
                }
            }
            catch (Exception)
            {
                // Unloadable assembly, broken registration, throwing provider: not our provider.
            }
        }

        return null;
    }

    /// <summary>
    /// Generates the document as JSON, using the request's own services.
    /// </summary>
    /// <remarks>
    /// Buffered rather than written straight to the response body: a generator that throws
    /// half-way through should produce a 500, not a 200 carrying half a document.
    /// </remarks>
    internal async Task<string> GenerateAsync(IServiceProvider requestServices)
    {
        object provider = requestServices.GetService(contract)
            ?? throw new InvalidOperationException(
                $"No {ProviderTypeName} is registered any more, so the spec for document "
                + $"'{documentName}' cannot be generated. It was registered when the pages were "
                + "mapped.");

        using StringWriter json = new();
        if (generate.Invoke(provider, [documentName, json]) is Task task)
        {
            await task.ConfigureAwait(false);
        }

        return json.ToString();
    }
}
