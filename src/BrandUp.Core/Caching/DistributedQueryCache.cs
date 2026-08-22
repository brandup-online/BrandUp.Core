using BrandUp.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace BrandUp.Caching
{
    /// <summary>
    /// <see cref="IQueryCache"/> over <see cref="IDistributedCache"/> (Redis, SQL Server, or any
    /// other provider registered in the container), serializing results with
    /// <see cref="IResultSerializer"/> — see
    /// <see cref="DomainBuilderExtensions.AddDistributedQueryCaching"/>. Follows the documented
    /// failure philosophy: a backend outage degrades reads to misses and writes to no-ops (logged),
    /// never failing the dispatch. Deserialized results are fresh copies, so the shared-instance
    /// caveat of the in-process cache does not apply.
    /// </summary>
    public sealed class DistributedQueryCache(IDistributedCache cache, IResultSerializer serializer, ILogger<DistributedQueryCache>? logger = null) : IQueryCache
    {
        readonly IDistributedCache cache = cache ?? throw new ArgumentNullException(nameof(cache));
        readonly IResultSerializer serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        /// <inheritdoc/>
        public async ValueTask<Result?> GetAsync(string key, Type resultType, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);

                return payload == null ? null : serializer.Deserialize(payload, resultType);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Distributed query cache read of key \"{CacheKey}\" failed; treated as a miss.", key);
                return null;
            }
        }

        /// <inheritdoc/>
        public async ValueTask SetAsync(string key, Result result, TimeSpan? duration, CancellationToken cancellationToken = default)
        {
            try
            {
                var entryOptions = new DistributedCacheEntryOptions();
                if (duration != null)
                    entryOptions.AbsoluteExpirationRelativeToNow = duration;

                await cache.SetAsync(key, serializer.Serialize(result), entryOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Distributed query cache write of key \"{CacheKey}\" failed; the result is served uncached.", key);
            }
        }

        /// <inheritdoc/>
        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            // Removal failures propagate: QueryCacheBehavior logs them without failing the command.
            return new ValueTask(cache.RemoveAsync(key, cancellationToken));
        }
    }
}
