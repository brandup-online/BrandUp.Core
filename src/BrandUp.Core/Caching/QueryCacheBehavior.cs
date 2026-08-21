using BrandUp.Behaviors;

namespace BrandUp.Caching
{
    /// <summary>
    /// Serves queries declaring <see cref="ICachedQuery"/> from <see cref="IQueryCache"/>, stores
    /// successful results, and removes the keys declared by <see cref="ICacheInvalidating"/>
    /// commands after the outermost command completes (after the transaction commit). Only
    /// successful results are cached; queries running inside a command are bypassed (they may
    /// observe uncommitted state). The cached <see cref="Result"/> instance is shared between
    /// callers - treat its data as read-only. A cache hit short-circuits behaviors registered
    /// after this one, so register caching after authorization-like behaviors.
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

                if (result is { IsSuccess: true })
                {
                    var cacheKeys = invalidating.InvalidateCacheKeys;

                    // Deferred to the completion of the outermost command - after the transaction
                    // commit, whatever position this behavior has in the pipeline - and discarded
                    // when an enclosing command fails. Post-success work is not cancellable on
                    // the caller's behalf.
                    var dispatchScope = CommandDispatchScope.Current;
                    if (dispatchScope != null)
                    {
                        dispatchScope.OnCompleted(async () =>
                        {
                            foreach (var cacheKey in cacheKeys)
                                await queryCache.RemoveAsync(cacheKey, CancellationToken.None).ConfigureAwait(false);
                        });
                    }
                    else
                    {
                        foreach (var cacheKey in cacheKeys)
                            await queryCache.RemoveAsync(cacheKey, CancellationToken.None).ConfigureAwait(false);
                    }
                }

                return result;
            }

            return await next().ConfigureAwait(false);
        }
    }
}
