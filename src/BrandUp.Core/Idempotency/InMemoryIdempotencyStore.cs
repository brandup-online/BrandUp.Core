using System.Collections.Concurrent;

namespace BrandUp.Idempotency
{
    /// <summary>
    /// Default in-process <see cref="IIdempotencyStore"/>. Completed keys are kept for the
    /// retention period (1 hour unless configured). An in-flight claim is bounded by the claim
    /// lease (10 minutes unless configured), so a crash or a discarded completion cannot block a
    /// key forever — at the price of a possible re-execution when a command outlives the lease;
    /// set the lease above the longest expected command duration. Expired entries are pruned by a
    /// background timer, off the request path, keeping claim latency flat regardless of how many
    /// keys are live. Size the retention consciously: every completed key holds its full
    /// <see cref="Result"/> (including data) in memory for the whole period, so steady-state
    /// memory is idempotent-command throughput × retention × payload size. Per-instance only —
    /// use a distributed implementation when deduplication must span instances.
    /// </summary>
    public sealed class InMemoryIdempotencyStore : IIdempotencyStore, IDisposable
    {
        readonly ConcurrentDictionary<string, Entry> entries = new();
        readonly TimeSpan retention;
        readonly TimeSpan claimLease;
        readonly TimeProvider timeProvider;
        readonly ITimer sweepTimer;

        /// <summary>
        /// Creates the store.
        /// </summary>
        /// <param name="retention">How long a completed key stays replayable; 1 hour by default.</param>
        /// <param name="claimLease">How long an in-flight claim blocks the key; 10 minutes by default.</param>
        /// <param name="timeProvider">Clock; the system clock by default.</param>
        public InMemoryIdempotencyStore(TimeSpan? retention = null, TimeSpan? claimLease = null, TimeProvider? timeProvider = null)
        {
            // Positive durations are also what keeps the sweep and the CAS loop safe: a reclaimed
            // entry always carries a strictly later expiry than the expired snapshot it replaces.
            this.retention = EnsurePositive(retention, nameof(retention)) ?? TimeSpan.FromHours(1);
            this.claimLease = EnsurePositive(claimLease, nameof(claimLease)) ?? TimeSpan.FromMinutes(10);
            this.timeProvider = timeProvider ?? TimeProvider.System;

            // Sweeping on a timer (instead of amortized inside claims) keeps the O(N) scan off
            // request threads and prunes even when idempotent traffic stops. The period tracks
            // the shortest expiry so short-lived test configurations still prune promptly.
            var sweepPeriod = TimeSpan.FromMinutes(1);
            if (this.retention < sweepPeriod)
                sweepPeriod = this.retention;
            if (this.claimLease < sweepPeriod)
                sweepPeriod = this.claimLease;

            sweepTimer = this.timeProvider.CreateTimer(static state => ((InMemoryIdempotencyStore)state!).Sweep(), this, sweepPeriod, sweepPeriod);
        }

        /// <inheritdoc/>
        public ValueTask<IdempotencyEntry?> TryClaimAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

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

        /// <inheritdoc/>
        public void Dispose()
        {
            sweepTimer.Dispose();
        }

        static TimeSpan? EnsurePositive(TimeSpan? value, string paramName)
        {
            if (value is { } duration && duration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(paramName, duration, "Duration must be positive.");

            return value;
        }

        void Sweep()
        {
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
