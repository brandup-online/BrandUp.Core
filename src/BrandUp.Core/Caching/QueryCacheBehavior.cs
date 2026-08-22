using System.Diagnostics;
using BrandUp.Behaviors;
using Microsoft.Extensions.Logging;

namespace BrandUp.Caching
{
    /// <summary>
    /// Serves queries declaring <see cref="ICachedQuery"/> from <see cref="IQueryCache"/>, stores
    /// successful results, and removes the keys declared by <see cref="ICacheInvalidating"/>
    /// commands after the outermost command completes (after the transaction commit). Only
    /// successful results are cached; queries running inside a command are bypassed (they may
    /// observe uncommitted state). The cached <see cref="Result"/> instance is shared between
    /// callers - treat its data as read-only. A cache hit short-circuits behaviors registered
    /// after this one, so register caching after authorization-like behaviors. The dispatch span
    /// carries a <c>brandup.cache</c> tag (hit/miss/bypass) for cached queries; an invalidation
    /// failure is logged, never surfaced to the command's caller.
    /// </summary>
    public sealed class QueryCacheBehavior(IQueryCache queryCache, ILogger<QueryCacheBehavior>? logger = null) : IDomainBehavior
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
                {
                    TagDispatchSpan("bypass");
                    return await next().ConfigureAwait(false);
                }

                var cached = await queryCache.GetAsync(cachedQuery.CacheKey, context.ResultType, cancellationToken).ConfigureAwait(false);

                // A wrong-shaped entry (cache-key collision between different query shapes) is
                // treated as a miss instead of poisoning the dispatch with a wrong-typed result.
                if (cached != null && context.ResultType.IsInstanceOfType(cached))
                {
                    TagDispatchSpan("hit");
                    return cached;
                }

                TagDispatchSpan("miss");

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
                    // Snapshot: the keys are removed after the handler returned, possibly at the
                    // completion of an enclosing command.
                    var cacheKeys = invalidating.InvalidateCacheKeys.ToArray();

                    // Deferred to the completion of the outermost command - after the transaction
                    // commit, whatever position this behavior has in the pipeline - and discarded
                    // when an enclosing command fails. Post-success work is not cancellable on
                    // the caller's behalf.
                    var dispatchScope = CommandDispatchScope.Current;
                    if (dispatchScope != null)
                        dispatchScope.OnCompleted(() => RemoveKeysAsync(cacheKeys));
                    else
                        await RemoveKeysAsync(cacheKeys).ConfigureAwait(false);
                }

                return result;
            }

            return await next().ConfigureAwait(false);
        }

        static void TagDispatchSpan(string outcome)
        {
            // The dispatch span exists only when a listener is attached to BrandUp.Domain;
            // without one Activity.Current is the host's ambient span (e.g. the HTTP request
            // activity) - tagging that would pollute foreign telemetry with a value that several
            // dispatches of one request would overwrite.
            if (Activity.Current is { } activity && activity.Source.Name == DomainDiagnostics.ActivitySourceName)
                activity.SetTag("brandup.cache", outcome);
        }

        async ValueTask RemoveKeysAsync(string[] cacheKeys)
        {
            // The command already succeeded (and committed): an invalidation failure is an
            // infrastructure problem to log, not a reason to surface the completed command as an
            // error - the same rule deferred event handlers follow.
            foreach (var cacheKey in cacheKeys)
            {
                try
                {
                    await queryCache.RemoveAsync(cacheKey, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger?.LogError(exception, "Cache invalidation of key \"{CacheKey}\" failed.", cacheKey);
                }
            }
        }
    }
}
