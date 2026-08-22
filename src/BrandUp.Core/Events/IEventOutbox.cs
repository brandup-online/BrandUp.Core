namespace BrandUp.Events
{
    /// <summary>
    /// Durable store for deferred domain events (transactional outbox). Enabled explicitly via
    /// <c>AddEventOutbox&lt;TOutbox&gt;</c> or <c>UseEventOutbox</c> — merely registering an implementation does not reroute events. When
    /// enabled, deferred events are enqueued here at publish time — inside the command's
    /// transaction when the store shares it — instead of being executed in process; a background
    /// processor reads them back after commit and delivers through
    /// <see cref="IDomainEventDispatcher"/>. Immediate handlers are unaffected. Designed to be
    /// paired with a transaction boundary: without one, events enqueued by a command that later
    /// fails are delivered anyway.
    /// </summary>
    public interface IEventOutbox
    {
        /// <summary>
        /// Persists the event for later delivery. Called once per published event that has at
        /// least one deferred handler.
        /// </summary>
        /// <param name="event">Event to persist.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task EnqueueAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Executes the deferred handlers of a stored event. Used by outbox processors after the
    /// event is read back from <see cref="IEventOutbox"/>. Registered as a scoped service by
    /// <c>AddDomain</c>.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Runs every deferred handler registered for the event's runtime type, sequentially in
        /// registration order. A handler exception propagates so the processor can retry the
        /// delivery — handlers must be idempotent, delivery is at-least-once.
        /// </summary>
        /// <param name="event">Event to dispatch.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task DispatchDeferredAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
    }
}
