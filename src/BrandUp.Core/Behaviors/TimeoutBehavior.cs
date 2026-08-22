namespace BrandUp.Behaviors
{
    /// <summary>
    /// Cancels dispatches of requests declaring <see cref="IDispatchTimeout"/> when the declared
    /// duration elapses, turning the cancellation into the cataloged
    /// <see cref="DomainErrors.DispatchTimeout"/> error; the caller's own cancellation still
    /// surfaces as <see cref="OperationCanceledException"/>. Downstream behaviors and the handler
    /// observe the narrowed token via <see cref="DomainBehaviorContext.CancellationToken"/>.
    /// Register before <c>AddTransactions</c> so the transaction lives inside the timeout: a
    /// timed-out attempt aborts on disposal and returns an error result, which also discards the
    /// attempt's deferred events.
    /// </summary>
    public sealed class TimeoutBehavior : IDomainBehavior
    {
        /// <inheritdoc/>
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            // Requests without a timeout pass through with no async state machine.
            if (context.Request is not IDispatchTimeout { Timeout: var timeout } || timeout <= TimeSpan.Zero)
                return next();

            return InvokeWithTimeoutAsync(context, next, timeout, cancellationToken);
        }

        static async Task<Result> InvokeWithTimeoutAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            // Each pipeline step reads the context token at invocation time, so narrowing it here
            // reaches every downstream behavior and the handler.
            var callerToken = context.CancellationToken;
            context.CancellationToken = timeoutSource.Token;
            try
            {
                return await next().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return context.CreateError([DomainErrors.DispatchTimeout.CreateError(context.Request.GetType().Name)]);
            }
            finally
            {
                context.CancellationToken = callerToken;
            }
        }
    }
}
