using BrandUp.Events;

namespace BrandUp.Testing
{
    /// <summary>
    /// Fake <see cref="IEventOutbox"/>: an in-memory FIFO queue of enqueued events with an
    /// explicit delivery step, letting a test assert what was enqueued and control when (and
    /// whether) delivery happens.
    /// </summary>
    public class TestEventOutbox : IEventOutbox
    {
        readonly List<IDomainEvent> enqueued = [];

        /// <summary>
        /// Events currently waiting for delivery, in enqueue order.
        /// </summary>
        public IReadOnlyList<IDomainEvent> Enqueued
        {
            get
            {
                lock (enqueued)
                    return [.. enqueued];
            }
        }

        /// <inheritdoc/>
        public Task EnqueueAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            lock (enqueued)
                enqueued.Add(@event);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Delivers the queued events in order through the dispatcher, the way an outbox
        /// processor would. A failing delivery keeps the event at the head of the queue and
        /// rethrows, matching at-least-once semantics.
        /// </summary>
        /// <param name="eventDispatcher">Dispatcher executing the deferred handlers.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Number of delivered events.</returns>
        public async Task<int> DeliverAsync(IDomainEventDispatcher eventDispatcher, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventDispatcher);

            var delivered = 0;
            while (true)
            {
                IDomainEvent next;
                lock (enqueued)
                {
                    if (enqueued.Count == 0)
                        break;
                    next = enqueued[0];
                }

                await eventDispatcher.DispatchDeferredAsync(next, cancellationToken).ConfigureAwait(false);

                // Enqueues only append, so the delivered event is still at the head.
                lock (enqueued)
                    enqueued.RemoveAt(0);
                delivered++;
            }

            return delivered;
        }

        /// <summary>
        /// Drops everything queued so far.
        /// </summary>
        public void Clear()
        {
            lock (enqueued)
                enqueued.Clear();
        }
    }
}
