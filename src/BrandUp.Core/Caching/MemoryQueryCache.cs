using Microsoft.Extensions.Caching.Memory;

namespace BrandUp.Caching
{
    /// <summary>
    /// Default in-process <see cref="IQueryCache"/> backed by <see cref="MemoryCache"/>. Bounded
    /// by default: without explicit options the cache holds at most
    /// <see cref="DefaultSizeLimit"/> entries (each entry counts as size 1), evicting older
    /// entries when full — parameterized cache keys (one per user, page, filter...) therefore
    /// cannot grow the process without limit. An entry whose query declares no
    /// <c>CacheDuration</c> has no expiration but stays evictable. Pass your own
    /// <see cref="MemoryCacheOptions"/> to change or remove the bound. Cached
    /// <see cref="Result"/> instances are shared between callers - treat their data as
    /// read-only. Swap for a custom implementation when cache coherence across instances is
    /// required.
    /// </summary>
    public sealed class MemoryQueryCache : IQueryCache, IDisposable
    {
        /// <summary>
        /// Entry-count bound applied when no <see cref="MemoryCacheOptions"/> are provided.
        /// </summary>
        public const int DefaultSizeLimit = 10_000;

        readonly MemoryCache cache;

        /// <summary>
        /// Creates the cache.
        /// </summary>
        /// <param name="options">Cache limits and expiration scanning settings; when omitted, a
        /// <see cref="MemoryCacheOptions.SizeLimit"/> of <see cref="DefaultSizeLimit"/> entries
        /// applies. Explicit options are taken as-is — leave <c>SizeLimit</c> unset there to opt
        /// into an unbounded cache deliberately.</param>
        public MemoryQueryCache(MemoryCacheOptions? options = null)
        {
            cache = new MemoryCache(options ?? new MemoryCacheOptions { SizeLimit = DefaultSizeLimit });
        }

        /// <inheritdoc/>
        public ValueTask<Result?> GetAsync(string key, Type resultType, CancellationToken cancellationToken = default)
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
            // The size limit bounds entry count, not bytes: a huge cached row list still counts
            // as 1. Size the limit (or per-entry cost) accordingly for large results.
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
