namespace BrandUp.Events
{
    /// <summary>
    /// The single point through which domain commands report facts that occurred.
    /// Registered as a scoped service by <c>AddDomain</c>. Replacing this registration with a custom
    /// implementation replaces the event routing only: the deferral semantics of
    /// <see cref="IDeferredDomainEventHandler{TEvent}"/> are provided by the built-in publisher,
    /// which alone observes command boundaries — a custom implementation receives no notification
    /// of command completion.
    /// </summary>
    public interface IDomainEventPublisher
    {
        /// <summary>
        /// Routes the event to the handlers registered for its runtime type (exact match; base-type
        /// registrations do not receive derived events). Handlers run sequentially in registration
        /// order; deferred handlers (<see cref="IDeferredDomainEventHandler{TEvent}"/>) published
        /// inside a domain command wait until the outermost command completes successfully.
        /// </summary>
        /// <param name="event">Event to publish.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="InvalidOperationException">
        /// No handlers are registered for the event type and <see cref="DomainOptions.RequireEventHandlers"/> is set.
        /// </exception>
        Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
    }
}
