namespace BrandUp.Caching
{
    /// <summary>
    /// Storage behind <see cref="QueryCacheBehavior"/>. The default is the in-process
    /// <see cref="MemoryQueryCache"/>; register a custom implementation for distributed scenarios.
    /// Failure philosophy: cache infrastructure failures must not fail dispatches - an
    /// implementation should degrade a backend outage to a miss in <see cref="GetAsync"/> and a
    /// no-op in <see cref="SetAsync"/> rather than throw. A throwing <see cref="RemoveAsync"/>
    /// during invalidation is caught and logged by the behavior, never surfaced to the command's
    /// caller.
    /// </summary>
    public interface IQueryCache
    {
        /// <summary>
        /// Returns the cached result for the key, or <see langword="null"/> when absent or expired.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="resultType">The exact <see cref="Result"/>-derived type the dispatch
        /// expects (e.g. <c>Result&lt;OrderModel&gt;</c>). Serializing implementations use it to
        /// rebuild the result and treat a mismatched entry as a miss; in-process implementations
        /// may ignore it — the behavior re-checks the shape.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask<Result?> GetAsync(string key, Type resultType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a result under the key.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="result">Result to store.</param>
        /// <param name="duration">Validity period; <see langword="null"/> means no expiration.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask SetAsync(string key, Result result, TimeSpan? duration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the key from the cache. Removing an absent key is a no-op.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
    }
}
