namespace Hlibz.Redoc.Extensions;

/// <summary>
/// One page's generated spec, kept for <see cref="ReDocDocumentsOptions.SpecCacheDuration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Requests that arrive while a generation is running wait for that one rather than starting
/// their own, so an expiry under load costs one generation, not one per request. A generation
/// that fails is not kept: the next request tries again, and the failure reaches only the
/// requests that were waiting on it.
/// </para>
/// <para>
/// The result is a <see cref="ReDocFile"/>, so a cached spec gets what every other file here
/// gets: a strong ETag per encoding, and brotli and gzip copies made once rather than per request.
/// </para>
/// </remarks>
internal sealed class ReDocSpecCache(OpenApiSpecSource source, TimeSpan duration, TimeProvider time)
{
    // An object rather than System.Threading.Lock, which net8.0 doesn't have.
    private readonly object gate = new();
    private Task<Entry>? current;

    public Task<ReDocFile> GetAsync(IServiceProvider requestServices)
    {
        Task<Entry> generation;
        lock (gate)
        {
            if (current is null
                || current.IsFaulted
                || current.IsCanceled
                || (current.IsCompletedSuccessfully && time.GetUtcNow() >= current.Result.Expires))
            {
                current = GenerateAsync(requestServices);
            }

            generation = current;
        }

        return FileOf(generation);
    }

    private static async Task<ReDocFile> FileOf(Task<Entry> generation) =>
        (await generation.ConfigureAwait(false)).File;

    private async Task<Entry> GenerateAsync(IServiceProvider requestServices)
    {
        string json = await source.GenerateAsync(requestServices).ConfigureAwait(false);

        // From when the document was finished, not when it was asked for: a slow generation
        // shouldn't eat into the time it is reused for.
        return new Entry(new ReDocFile(json, "application/json"), time.GetUtcNow() + duration);
    }

    private sealed record Entry(ReDocFile File, DateTimeOffset Expires);
}
