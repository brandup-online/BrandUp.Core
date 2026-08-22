using System.Collections.Concurrent;

namespace BrandUp.Idempotency
{
    /// <summary>
    /// Default in-process <see cref="IIdempotencyStore"/>. Completed keys are kept for the
    /// retention period (24 hours unless configured). An in-flight claim is bounded by the claim
    /// lease (10 minutes unless configured), so a crash or a discarded completion cannot block a
    /// key forever — at the price of a possible re-execution when a command outlives the lease;
    /// set the lease above the longest expected command duration. Expired entries are pruned
    /// opportunistically, keeping the store bounded under the normal fresh-key-per-request
    /// pattern. Per-instance only — use a distributed implementation when deduplication must span
    /// instances.
    /// </summary>
    public sealed class InMemoryIdempotencyStore(TimeSpan? retention = null, TimeSpan? claimLease = null, TimeProvider? timeProvider = null) : IIdempotencyStore
    {
        readonly ConcurrentDictionary<string, Entry> entries = new();

        // Positive durations are also what keeps the sweep and the CAS loop safe: a reclaimed
        // entry always carries a strictly later expiry than the expired snapshot it replaces.
        readonly TimeSpan retention = EnsurePositive(retention, nameof(retention)) ?? TimeSpan.FromHours(24);
        readonly TimeSpan claimLease = EnsurePositive(claimLease, nameof(claimLease)) ?? TimeSpan.FromMinutes(10);
        readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
        int operationCount;

        /// <inheritdoc/>
        public ValueTask<IdempotencyEntry?> TryClaimAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            Sweep();

            while (true)
            {
                var inFlight = new Entry(IdempotencyEntry.InFlight, timeProvider.GetUtcNow() + claimLease);
                if (entries.TryAdd(key, inFlight))
                    return ValueTask.FromResult<IdempotencyEntry?>(null);

                if (!entries.TryGetValue(key, out var existing))
                    continue; // released between TryAdd and TryGetValue - claim again

                // An expired entry - completed past its retention, or an abandoned claim past its
                // lease - is reclaimable.
                if (existing.ExpiresAt <= timeProvider.GetUtcNow())
                {
                    if (entries.TryUpdate(key, inFlight, existing))
                        return ValueTask.FromResult<IdempotencyEntry?>(null);

                    continue; // lost the race - re-evaluate
                }

                return ValueTask.FromResult<IdempotencyEntry?>(existing.Value);
            }
        }

        /// <inheritdoc/>
        public ValueTask CompleteAsync(string key, Result result, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(result);

            entries[key] = new Entry(IdempotencyEntry.Completed(result), timeProvider.GetUtcNow() + retention);

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            entries.TryRemove(key, out _);

            return ValueTask.CompletedTask;
        }

        static TimeSpan? EnsurePositive(TimeSpan? value, string paramName)
        {
            if (value is { } duration && duration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(paramName, duration, "Duration must be positive.");

            return value;
        }

        // Amortized cleanup on every 128th operation: distinct keys are the normal pattern (a
        // fresh key per request), so waiting for the same key to come back would never free
        // anything and the dictionary would grow for the process lifetime.
        void Sweep()
        {
            if ((Interlocked.Increment(ref operationCount) & 127) != 0)
                return;

            var now = timeProvider.GetUtcNow();
            foreach (var pair in entries)
            {
                // Conditional remove: only the exact expired entry is dropped, never a fresh
                // claim that replaced it concurrently.
                if (pair.Value.ExpiresAt <= now)
                    ((ICollection<KeyValuePair<string, Entry>>)entries).Remove(pair);
            }
        }

        sealed record Entry(IdempotencyEntry Value, DateTimeOffset ExpiresAt);
    }
}
