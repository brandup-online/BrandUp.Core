namespace BrandUp.Events
{
    /// <summary>
    /// Reacts to a domain event. An event may have any number of handlers; they are executed
    /// sequentially in registration order. Events are routed by their runtime type (exact match).
    /// Handlers are registered via <see cref="DomainOptions.AddEvent{THandler}"/>.
    /// </summary>
    /// <typeparam name="TEvent">Type of the handled event.</typeparam>
    public interface IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        /// <summary>
        /// Handles the event. An exception thrown here propagates to the publisher; a handler whose
        /// failure must not fail the operation should catch and log it itself.
        /// </summary>
        /// <param name="event">Event to handle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Same contract as <see cref="IDomainEventHandler{TEvent}"/>, but execution is deferred until
    /// the outermost domain command completes successfully; if the command (or any enclosing
    /// command) fails, the deferred execution is discarded. When the event is published outside of
    /// a command, the handler runs immediately — there is no completion to wait for.
    /// <para>
    /// Unlike immediate handlers, an exception from a deferred handler is logged and does not fail
    /// the already-completed command, nor does it stop the remaining deferred handlers. Deferred
    /// execution receives <see cref="CancellationToken.None"/>: by then the command has succeeded
    /// and its caller may already be gone, so the work is not cancellable on the caller's behalf —
    /// a handler needing a timeout manages its own.
    /// </para>
    /// <para>
    /// The command boundary follows the async flow within one publisher instance. A command
    /// dispatched through another service scope or container forms its own boundary: its deferred
    /// handlers run at its own completion, from its own service provider. A publish that reaches
    /// an already-ended command boundary — e.g. from a fire-and-forget task started inside the
    /// command — executes the handler immediately instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TEvent">Type of the handled event.</typeparam>
    public interface IDeferredDomainEventHandler<TEvent> : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
    }
}
