using BrandUp.Behaviors;

namespace BrandUp.Caching
{
    /// <summary>
    /// Serves queries declaring <see cref="ICachedQuery"/> from <see cref="IQueryCache"/>, stores
    /// successful results, and removes the keys declared by <see cref="ICacheInvalidating"/>
    /// commands after they succeed. Only successful results are cached; queries running inside a
    /// command are bypassed (they may observe uncommitted state). The cached <see cref="Result"/>
    /// instance is shared between callers - treat its data as read-only.
    /// </summary>
    public class QueryCacheBehavior(IQueryCache queryCache) : IDomainBehavior
    {
        readonly IQueryCache queryCache = queryCache ?? throw new ArgumentNullException(nameof(queryCache));

        /// <inheritdoc/>
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            if (context.IsQuery && context.Request is ICachedQuery cachedQuery)
            {
                // A query inside a command runs on the command's ambient session and may observe
                // uncommitted state: neither serve nor store the cache for it.
                if (context.IsInsideCommand)
                    return await next().ConfigureAwait(false);

                var cached = await queryCache.GetAsync(cachedQuery.CacheKey, cancellationToken).ConfigureAwait(false);

                // A wrong-shaped entry (cache-key collision between different query shapes) is
                // treated as a miss instead of poisoning the dispatch with a wrong-typed result.
                if (cached != null && context.ResultType.IsInstanceOfType(cached))
                    return cached;

                var result = await next().ConfigureAwait(false);

                // The query already succeeded: storing the result is post-success work, not
                // cancellable on the caller's behalf.
                if (result is { IsSuccess: true })
                    await queryCache.SetAsync(cachedQuery.CacheKey, result, cachedQuery.CacheDuration, CancellationToken.None).ConfigureAwait(false);

                return result;
            }

            if (context.IsCommand && context.Request is ICacheInvalidating invalidating)
            {
                var result = await next().ConfigureAwait(false);

                // AddQueryCaching keeps this behavior outside TransactionBehavior, so for the
                // command's own transaction this runs after the commit. Post-success work is not
                // cancellable on the caller's behalf.
                if (result is { IsSuccess: true })
                {
                    foreach (var cacheKey in invalidating.InvalidateCacheKeys)
                        await queryCache.RemoveAsync(cacheKey, CancellationToken.None).ConfigureAwait(false);
                }

                return result;
            }

            return await next().ConfigureAwait(false);
        }
    }
}
