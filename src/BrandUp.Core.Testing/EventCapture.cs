using BrandUp.Events;

namespace BrandUp.Testing
{
    /// <summary>
    /// Collects domain events published during a test, in publish order. Register it as a
    /// singleton (done by <see cref="DomainTestHost"/>) and subscribe per event type with
    /// <see cref="DomainOptionsTestingExtensions.CaptureEvent{TEvent}"/> — capture runs as an
    /// ordinary immediate handler, so it sees the event exactly when it is published.
    /// </summary>
    public sealed class EventCapture
    {
        readonly List<IDomainEvent> events = [];

        /// <summary>
        /// All captured events, in publish order.
        /// </summary>
        public IReadOnlyList<IDomainEvent> All
        {
            get
            {
                lock (events)
                    return [.. events];
            }
        }

        /// <summary>
        /// Captured events of the given type, in publish order.
        /// </summary>
        /// <typeparam name="TEvent">Event type to filter by.</typeparam>
        public IReadOnlyList<TEvent> OfType<TEvent>()
            where TEvent : IDomainEvent
        {
            lock (events)
                return [.. events.OfType<TEvent>()];
        }

        /// <summary>
        /// Asserts exactly one event of the type was captured and returns it.
        /// </summary>
        /// <typeparam name="TEvent">Expected event type.</typeparam>
        /// <exception cref="DomainAssertException">Zero or several events of the type were captured.</exception>
        public TEvent AssertSingle<TEvent>()
            where TEvent : IDomainEvent
        {
            var captured = OfType<TEvent>();
            if (captured.Count != 1)
                throw DomainAssert.Failure($"Expected exactly one {typeof(TEvent).Name} event, but {captured.Count} were captured.");

            return captured[0];
        }

        /// <summary>
        /// Asserts at least one event of the type (optionally matching the predicate) was captured.
        /// </summary>
        /// <typeparam name="TEvent">Expected event type.</typeparam>
        /// <param name="predicate">Optional condition the event must satisfy.</param>
        /// <exception cref="DomainAssertException">No matching event was captured.</exception>
        public void AssertPublished<TEvent>(Func<TEvent, bool>? predicate = null)
            where TEvent : IDomainEvent
        {
            var captured = OfType<TEvent>();
            if (predicate == null ? captured.Count == 0 : !captured.Any(predicate))
                throw DomainAssert.Failure($"Expected a {typeof(TEvent).Name} event{(predicate != null ? " matching the predicate" : string.Empty)}, but none was captured.");
        }

        /// <summary>
        /// Asserts no event of the type was captured.
        /// </summary>
        /// <typeparam name="TEvent">Event type expected to be absent.</typeparam>
        /// <exception cref="DomainAssertException">An event of the type was captured.</exception>
        public void AssertNotPublished<TEvent>()
            where TEvent : IDomainEvent
        {
            var captured = OfType<TEvent>();
            if (captured.Count > 0)
                throw DomainAssert.Failure($"Expected no {typeof(TEvent).Name} events, but {captured.Count} were captured.");
        }

        /// <summary>
        /// Forgets everything captured so far.
        /// </summary>
        public void Clear()
        {
            lock (events)
                events.Clear();
        }

        internal void Add(IDomainEvent @event)
        {
            lock (events)
                events.Add(@event);
        }
    }

    /// <summary>
    /// The immediate handler behind <see cref="DomainOptionsTestingExtensions.CaptureEvent{TEvent}"/>.
    /// </summary>
    /// <typeparam name="TEvent">Captured event type.</typeparam>
    public sealed class CaptureEventHandler<TEvent>(EventCapture capture) : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        /// <inheritdoc/>
        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            capture.Add(@event);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// <see cref="DomainOptions"/> extensions for tests.
    /// </summary>
    public static class DomainOptionsTestingExtensions
    {
        /// <summary>
        /// Subscribes <see cref="EventCapture"/> to the event type. Requires an
        /// <see cref="EventCapture"/> singleton in the service collection
        /// (registered automatically by <see cref="DomainTestHost"/>).
        /// </summary>
        /// <typeparam name="TEvent">Event type to capture.</typeparam>
        /// <param name="options">Domain options.</param>
        /// <returns>The same options, for chaining.</returns>
        public static DomainOptions CaptureEvent<TEvent>(this DomainOptions options)
            where TEvent : IDomainEvent
        {
            ArgumentNullException.ThrowIfNull(options);

            return options.AddEvent<CaptureEventHandler<TEvent>>();
        }
    }
}
