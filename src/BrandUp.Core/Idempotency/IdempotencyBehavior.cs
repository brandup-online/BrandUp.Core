using BrandUp.Behaviors;
using Microsoft.Extensions.Logging;

namespace BrandUp.Idempotency
{
    /// <summary>
    /// Deduplicates commands declaring <see cref="IIdempotentCommand"/>: a repeated dispatch with
    /// a completed key replays the stored result without reaching the handler; a dispatch while
    /// the key is in flight fails with <see cref="DomainErrors.DuplicateRequest"/>. The key is
    /// marked completed only after the outermost command finishes successfully — after the
    /// transaction commit — so a rolled-back command never records a replayable outcome; a failed
    /// or throwing command releases the key for retry. Nested command dispatches are exempt (the
    /// outermost dispatch owns deduplication). Register before <c>AddTransactions</c> so a replay
    /// short-circuits without opening a transaction, and after no behavior that rewrites a
    /// success into a failure. When the completion record is lost anyway - an outer behavior
    /// throws after the success, or the store's <see cref="IIdempotencyStore.CompleteAsync"/>
    /// fails post-commit - the key stays claimed until the store's claim lease frees it (see
    /// <see cref="InMemoryIdempotencyStore"/>), so retries recover instead of being rejected
    /// forever.
    /// </summary>
    public sealed class IdempotencyBehavior(IIdempotencyStore store, ILogger<IdempotencyBehavior>? logger = null) : IDomainBehavior
    {
        readonly IIdempotencyStore store = store ?? throw new ArgumentNullException(nameof(store));

        /// <inheritdoc/>
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            // Queries, nested commands and non-idempotent commands pass through with no async
            // state machine.
            if (!context.IsCommand
                || context.IsInsideCommand
                || context.Request is not IIdempotentCommand { IdempotencyKey: { Length: > 0 } key })
            {
                return next();
            }

            return InvokeCoreAsync(key, context, next, cancellationToken);
        }

        async Task<Result> InvokeCoreAsync(string key, DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken)
        {
            var existing = await store.TryClaimAsync(key, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                // Exact-type match: a stored result of a different dispatch shape (one key
                // reused across command types) is not replayable - report the duplicate instead
                // of serving a foreign command's outcome. IsInstanceOfType would be too loose
                // here: every Result<T> is a Result.
                if (existing is { IsCompleted: true, Result: { } stored } && stored.GetType() == context.ResultType)
                    return stored;

                return context.CreateError([DomainErrors.DuplicateRequest.CreateError(key)]);
            }

            try
            {
                var result = await next().ConfigureAwait(false);

                if (result is { IsSuccess: true })
                {
                    // Recorded at the completion of the outermost command - after the transaction
                    // commit - so a rolled-back command never becomes replayable. The scope runs
                    // the action only when the final dispatch result is successful; a behavior
                    // that rewrites this success into a failure would leave the key claimed -
                    // hence the documented ordering rule.
                    var dispatchScope = CommandDispatchScope.Current;
                    if (dispatchScope != null)
                        dispatchScope.OnCompleted(() => CompleteBoundedAsync(key, result));
                    else
                        await CompleteBoundedAsync(key, result).ConfigureAwait(false);
                }
                else
                {
                    await ReleaseBoundedAsync(key).ConfigureAwait(false);
                }

                return result;
            }
            catch
            {
                await ReleaseBoundedAsync(key).ConfigureAwait(false);
                throw;
            }
        }

        // Post-decision store calls are bounded (see PostDecision): a hung distributed store must
        // not stall an already-completed command. A lost record is safe either way - the claim
        // lease frees the key.
        async ValueTask CompleteBoundedAsync(string key, Result result)
        {
            using var timeoutSource = PostDecision.CreateTimeoutSource();
            try
            {
                await store.CompleteAsync(key, result, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Idempotency completion of key \"{IdempotencyKey}\" failed; the key stays claimed until its lease expires.", key);
            }
        }

        async ValueTask ReleaseBoundedAsync(string key)
        {
            using var timeoutSource = PostDecision.CreateTimeoutSource();
            try
            {
                await store.ReleaseAsync(key, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Idempotency release of key \"{IdempotencyKey}\" failed; the key stays claimed until its lease expires.", key);
            }
        }
    }
}
