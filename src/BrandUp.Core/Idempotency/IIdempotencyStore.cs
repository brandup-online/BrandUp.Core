namespace BrandUp.Idempotency
{
    /// <summary>
    /// Declares that a command deduplicates by an idempotency key. Requires
    /// <see cref="IdempotencyBehavior"/> (see
    /// <see cref="DomainBuilderExtensions.AddIdempotency(IDomainBuilder)"/>): a repeated dispatch
    /// with the key of an already-completed command replays the stored result without running the
    /// handler; a dispatch while the key is in flight fails with
    /// <see cref="DomainErrors.DuplicateRequest"/>.
    /// </summary>
    public interface IIdempotentCommand
    {
        /// <summary>
        /// Key identifying the operation across retries (e.g. a client-generated request id).
        /// An empty or <see langword="null"/> key disables deduplication for the dispatch.
        /// </summary>
        string? IdempotencyKey { get; }
    }

    /// <summary>
    /// State of an idempotency key as known by the <see cref="IIdempotencyStore"/>.
    /// </summary>
    public sealed class IdempotencyEntry
    {
        IdempotencyEntry(Result? result)
        {
            Result = result;
        }

        /// <summary>
        /// The stored outcome of the completed command; <see langword="null"/> while the command
        /// is still in flight.
        /// </summary>
        public Result? Result { get; }

        /// <summary>
        /// <see langword="true"/> when the command completed and <see cref="Result"/> holds its outcome.
        /// </summary>
        public bool IsCompleted => Result != null;

        /// <summary>
        /// An entry for a command that is still being processed.
        /// </summary>
        public static IdempotencyEntry InFlight { get; } = new(null);

        /// <summary>
        /// An entry for a completed command with its stored outcome.
        /// </summary>
        /// <param name="result">Outcome to replay for repeated dispatches.</param>
        public static IdempotencyEntry Completed(Result result)
        {
            ArgumentNullException.ThrowIfNull(result);

            return new IdempotencyEntry(result);
        }
    }

    /// <summary>
    /// Storage behind <see cref="IdempotencyBehavior"/>. The default is the in-process
    /// <see cref="InMemoryIdempotencyStore"/>; a distributed implementation shares keys across
    /// instances. The stored <see cref="Result"/> instance is shared between callers — treat its
    /// data as read-only. Implementations must bound an in-flight claim with a lease/TTL: a
    /// process dying between <see cref="TryClaimAsync"/> and
    /// <see cref="CompleteAsync"/>/<see cref="ReleaseAsync"/> would otherwise block the key
    /// forever.
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Atomically claims the key for a new execution. Returns <see langword="null"/> when the
        /// key was free and is now claimed (the caller proceeds and must end with
        /// <see cref="CompleteAsync"/> or <see cref="ReleaseAsync"/>); otherwise the existing
        /// entry — in flight or completed.
        /// </summary>
        /// <param name="key">Idempotency key.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask<IdempotencyEntry?> TryClaimAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores the outcome of the successfully completed command under the claimed key.
        /// </summary>
        /// <param name="key">Idempotency key.</param>
        /// <param name="result">Outcome to replay for repeated dispatches.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask CompleteAsync(string key, Result result, CancellationToken cancellationToken = default);

        /// <summary>
        /// Frees the claimed key after a failed execution, so a retry can run.
        /// </summary>
        /// <param name="key">Idempotency key.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default);
    }
}
