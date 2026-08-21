namespace BrandUp.Caching
{
    /// <summary>
    /// Storage behind <see cref="QueryCacheBehavior"/>. The default is the in-process
    /// <see cref="MemoryQueryCache"/>; register a custom implementation for distributed scenarios.
    /// </summary>
    public interface IQueryCache
    {
        /// <summary>
        /// Returns the cached result for the key, or <see langword="null"/> when absent or expired.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask<Result?> GetAsync(string key, CancellationToken cancellationToken = default);

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
