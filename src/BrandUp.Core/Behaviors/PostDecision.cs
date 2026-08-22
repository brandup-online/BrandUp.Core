namespace BrandUp.Behaviors
{
    /// <summary>
    /// Shared bound for post-decision work: cache writes, cache invalidations and idempotency
    /// records run after the dispatch outcome is decided, so they are not cancellable on the
    /// caller's behalf — but they also must not hold an already-completed dispatch hostage to a
    /// degraded backend's client timeout. The bound turns a hung backend into a logged, bounded
    /// delay instead of an unpredictable multi-second stall on a "successful" response.
    /// </summary>
    internal static class PostDecision
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        public static CancellationTokenSource CreateTimeoutSource() => new(Timeout);
    }
}
