using Microsoft.Extensions.Caching.Memory;

namespace BrandUp.Caching
{
    /// <summary>
    /// Default in-process <see cref="IQueryCache"/> backed by
    /// <see cref="MemoryCache"/>, giving expiration-driven eviction and
    /// optional size limits (via <see cref="MemoryCacheOptions"/>) instead of unbounded growth.
    /// Cached <see cref="Result"/> instances are shared between callers - treat their data as
    /// read-only. Swap for a custom implementation when cache coherence across instances is
    /// required.
    /// </summary>
    public class MemoryQueryCache : IQueryCache, IDisposable
    {
        readonly MemoryCache cache;

        /// <summary>
        /// Creates the cache.
        /// </summary>
        /// <param name="options">Optional cache limits and expiration scanning settings.</param>
        public MemoryQueryCache(MemoryCacheOptions? options = null)
        {
            cache = new MemoryCache(options ?? new MemoryCacheOptions());
        }

        /// <inheritdoc/>
        public ValueTask<Result?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);

            return ValueTask.FromResult(cache.TryGetValue(key, out Result? result) ? result : null);
        }

        /// <inheritdoc/>
        public ValueTask SetAsync(string key, Result result, TimeSpan? duration, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(result);

            using var entry = cache.CreateEntry(key);
            entry.Value = result;
            entry.Size = 1;
            if (duration.HasValue)
                entry.AbsoluteExpirationRelativeToNow = duration.Value;

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);

            cache.Remove(key);

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            cache.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
