using Microsoft.AspNetCore.Builder;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// What <see cref="ReDocDocumentsBuilderExtensions.MapReDocDocuments"/> returns: a convention
/// builder over every endpoint it mapped, and, through <see cref="ForDocument"/>, over one
/// document's endpoints on their own.
/// </summary>
/// <remarks>
/// <para>
/// Conventions applied to this builder reach everything — every page with its stylesheet and
/// scripts, the shared ReDoc bundle, and the redirect from the route prefix. That is what you want
/// for "the documentation is internal".
/// </para>
/// <para>
/// <see cref="ForDocument"/> is for the other common shape, one public document and the rest not.
/// It deliberately does <em>not</em> include the shared bundle: the bundle is one endpoint for the
/// whole site, so gating it along with a single document would stop every other page rendering,
/// and it is ReDoc's own published JavaScript rather than anything of yours. Gate the pages, not
/// the library.
/// </para>
/// <para>
/// Hiding a gated document from the other pages' pickers is a separate, deliberate step:
/// <see cref="ReDocDocument.Unlisted"/>. Authorization doesn't imply it, because the picker is
/// built once at startup and knows nothing about who is asking.
/// </para>
/// </remarks>
public sealed class ReDocDocumentsConventionBuilder : IEndpointConventionBuilder
{
    private readonly Group everything;
    private readonly IReadOnlyDictionary<string, Group> byDocument;

    internal ReDocDocumentsConventionBuilder(
        IReadOnlyList<IEndpointConventionBuilder> everything,
        IReadOnlyDictionary<string, Group> byDocument)
    {
        this.everything = new Group(everything);
        this.byDocument = byDocument;
    }

    /// <inheritdoc />
    public void Add(Action<EndpointBuilder> convention) => everything.Add(convention);

    /// <inheritdoc />
    public void Finally(Action<EndpointBuilder> finalConvention) =>
        everything.Finally(finalConvention);

    /// <summary>
    /// A convention builder over one document's own endpoints: its page, stylesheet and scripts,
    /// and nothing else. Use it to put a single document behind a policy.
    /// </summary>
    /// <param name="documentName">
    /// The <see cref="ReDocDocument.Name"/> given to <c>MapReDocDocuments</c>, matched
    /// case-insensitively — the same way document names are matched everywhere else.
    /// </param>
    /// <returns>A convention builder for that document's endpoints.</returns>
    /// <exception cref="ArgumentException">
    /// No document of that name was mapped. Thrown rather than ignored, because the alternative is
    /// a <c>RequireAuthorization</c> that silently protects nothing.
    /// </exception>
    public IEndpointConventionBuilder ForDocument(string documentName)
    {
        ArgumentNullException.ThrowIfNull(documentName);

        if (byDocument.TryGetValue(documentName, out Group? group))
        {
            return group;
        }

        string known = string.Join(", ", byDocument.Keys.Order().Select(name => $"'{name}'"));
        throw new ArgumentException(
            $"MapReDocDocuments mapped no document named '{documentName}'. Mapped documents: "
            + $"{known}.",
            nameof(documentName));
    }

    /// <summary>Fans a convention out over a fixed set of endpoints.</summary>
    internal sealed class Group(IReadOnlyList<IEndpointConventionBuilder> parts)
        : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            foreach (IEndpointConventionBuilder part in parts)
            {
                part.Add(convention);
            }
        }

        public void Finally(Action<EndpointBuilder> finalConvention)
        {
            foreach (IEndpointConventionBuilder part in parts)
            {
                part.Finally(finalConvention);
            }
        }
    }
}
