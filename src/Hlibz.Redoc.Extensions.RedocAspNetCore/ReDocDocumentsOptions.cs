using Redoc.AspNetCore;

namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Options for <see cref="ReDocDocumentsBuilderExtensions.UseReDocDocuments"/>. Everything but
/// <see cref="ConfigureReDoc"/> comes from <see cref="ReDocDocumentsOptionsBase"/> and is shared
/// with the other hosts.
/// </summary>
public sealed class ReDocDocumentsOptions : ReDocDocumentsOptionsBase
{
    /// <summary>
    /// Extra configuration applied to every document's <see cref="ReDocOptions"/>, e.g.
    /// <c>(redoc, _) =&gt; redoc.UseDarkTheme()</c>. Runs after the route, spec URL and title are
    /// set. It must not change <see cref="ReDocOptions.RoutePrefix"/>; set
    /// <see cref="ReDocDocument.Route"/> instead, so the picker's links know every page's route.
    /// </summary>
    public Action<ReDocOptions, ReDocDocument>? ConfigureReDoc { get; set; }
}
