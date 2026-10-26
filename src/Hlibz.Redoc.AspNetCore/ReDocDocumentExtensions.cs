namespace Hlibz.Redoc.Extensions;

/// <summary>
/// Declares a document internal, on the one host that can act on it.
/// </summary>
public static class ReDocDocumentExtensions
{
    /// <summary>
    /// Puts this document's page <em>and the OpenAPI document it renders</em> behind an
    /// authorization policy, and keeps it out of the other pages' pickers.
    /// </summary>
    /// <param name="document">The document to gate.</param>
    /// <param name="policyNames">
    /// Policies the viewer must satisfy, as registered with <c>AddAuthorizationBuilder</c>. Pass
    /// none to require nothing more than an authenticated user, the same as endpoint routing's own
    /// <c>RequireAuthorization()</c>.
    /// </param>
    /// <returns>
    /// The document to hand to <c>MapReDocDocuments</c>, carrying the policies. Declared as
    /// <see cref="ReDocDocument"/> so it drops straight into the collection you were building.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the whole of per-document authorization, because the three things it does are only
    /// ever wanted together. <c>MapReDocDocuments</c> puts the policies on every endpoint of this
    /// document's own — its page, stylesheet, scripts and spec — so the lock covers the document
    /// and not just the page that renders it. The spec is served from beside the page whether or
    /// not <see cref="ReDocDocumentsOptions.ServeSpecs"/> is on, since a policy can only cover a
    /// spec this package serves. And the document drops out of every other page's picker, so a
    /// public dropdown doesn't name your internal APIs to anyone who loads it.
    /// </para>
    /// <para>
    /// One gated document makes <em>every</em> page in the same call serve its own spec, not just
    /// the gated one. The public pages can't be left reading <c>MapOpenApi()</c>'s endpoint,
    /// because that one endpoint serves every document including this one — so the site would
    /// have the lock and the open window both. It means a site with a gated document needs no
    /// <c>MapOpenApi()</c> at all, and that every document in the call has to be one a registered
    /// provider offers, or have its own <see cref="ReDocDocument.SpecUrl"/>.
    /// </para>
    /// <para>
    /// What it cannot do is close a spec somebody else serves. If the app also calls
    /// <c>MapOpenApi()</c> or <c>UseSwagger()</c>, those endpoints are untouched and still answer
    /// anyone — removing or gating them is a separate, deliberate step.
    /// </para>
    /// <para>
    /// Only the pages are gated, never the shared ReDoc bundle: one endpoint serves it to the whole
    /// site, so gating it with a single document would stop every other page rendering, and it is
    /// ReDoc's own published JavaScript rather than anything of yours.
    /// </para>
    /// <para>
    /// For anything else endpoint routing can do to one document — output caching, rate limiting, a
    /// policy object rather than a name, or gating a document while leaving it listed — use
    /// <c>MapReDocDocuments(...).ForDocument(name)</c> instead. The two compose: this method's
    /// policies and that builder's conventions both land on the same endpoints.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> or <paramref name="policyNames"/> is <see langword="null"/>.
    /// </exception>
    public static ReDocDocument RequireAuthorization(
        this ReDocDocument document, params string[] policyNames)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(policyNames);

        // Called twice, the policies add up, the way they would on an endpoint builder. Rebuilding
        // from the base view alone would quietly drop the first call's.
        IReadOnlyList<string> policies = document is ReDocGatedDocument already
            ? [.. already.Policies, .. policyNames]
            : [.. policyNames];

        return new ReDocGatedDocument(
            document.Name,
            document.DisplayName,
            document.SpecUrl,
            document.Route,
            document.Group,
            policies);
    }
}

/// <summary>
/// A document that <see cref="ReDocDocumentExtensions.RequireAuthorization"/> has gated.
/// </summary>
/// <remarks>
/// <para>
/// Internal on purpose, and the reason <see cref="ReDocDocument"/> is unsealed: the type exists in
/// this package only, so a consumer of an adapter — where pages are middleware and no policy can
/// be attached to anything — cannot write the gating at all. That is better than accepting it and
/// throwing, and far better than accepting it and doing nothing.
/// </para>
/// <para>
/// <c>Unlisted</c> is passed to the base rather than offered as a parameter: a gated document is
/// always hidden from the other pages' pickers, and the way to have one listed anyway is the
/// unopinionated form, a plain <see cref="ReDocDocument"/> plus
/// <c>ForDocument(name).RequireAuthorization()</c>.
/// </para>
/// </remarks>
internal sealed record ReDocGatedDocument(
    string Name,
    string? DisplayName,
    string? SpecUrl,
    string? Route,
    string? Group,
    IReadOnlyList<string> Policies)
    : ReDocDocument(Name, DisplayName, SpecUrl, Route, Unlisted: true, Group);
