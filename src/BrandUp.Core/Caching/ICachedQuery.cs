namespace BrandUp.Caching
{
    /// <summary>
    /// Declares that a query's successful result may be cached. Requires
    /// <see cref="QueryCacheBehavior"/> (see <see cref="DomainBuilderExtensions.AddQueryCaching(Builder.IDomainBuilder)"/>).
    /// </summary>
    public interface ICachedQuery
    {
        /// <summary>
        /// Cache key of this query instance. Must be unique across query shapes — two different
        /// queries sharing a key would return each other's results.
        /// </summary>
        string CacheKey { get; }

        /// <summary>
        /// How long the cached result stays valid; <see langword="null"/> means no expiration
        /// (until invalidated via <see cref="ICacheInvalidating"/> or evicted by the cache).
        /// </summary>
        TimeSpan? CacheDuration { get; }
    }

    /// <summary>
    /// Declares that a successful execution of this command invalidates the given cache keys.
    /// </summary>
    public interface ICacheInvalidating
    {
        /// <summary>
        /// Cache keys removed after the command succeeds.
        /// </summary>
        IEnumerable<string> InvalidateCacheKeys { get; }
    }
}
