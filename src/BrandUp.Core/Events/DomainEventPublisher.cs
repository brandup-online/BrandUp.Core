using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BrandUp.Events
{
    internal class DomainEventPublisher(IOptions<DomainOptions> options, IServiceProvider serviceProvider, ILogger<DomainEventPublisher>? logger = null) : IDomainEventPublisher, IDomainEventDispatcher
    {
        // Per-async-flow stack of command scopes: parallel commands and nested commands are isolated
        // by construction, and no scope outlives the dispatch that created it (an abandoned scope is
        // simply collected with its dispatch context). The stack is process-wide, so every scope
        // records its owning publisher: a publisher only defers into (and links to) its own scopes —
        // a command dispatched through another container or service scope forms its own boundary,
        // keeping its handlers on its own service provider.
        static readonly AsyncLocal<CommandScope?> currentScope = new();

        readonly DomainOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        readonly ILogger logger = logger ?? NullLogger<DomainEventPublisher>.Instance;
        IEventOutbox? outbox;

        #region IDomainEventPublisher members

        public async Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var eventType = @event.GetType();
            if (!options.TryGetEventHandlers(eventType, out var eventHandlers))
            {
                if (options.RequireEventHandlers)
                    throw new InvalidOperationException($"Not found event handlers by event \"{eventType.AssemblyQualifiedName}\".");
                return;
            }

            var scope = currentScope.Value;
            if (scope != null && !scope.IsOwnedBy(this))
                scope = null;

            // With the outbox enabled, deferred delivery goes through durable storage: the
            // enqueue happens at publish time so it lands inside the command's transaction.
            var eventOutbox = ResolveOutbox();
            if (eventOutbox != null && HasDeferredHandler(eventHandlers))
                await eventOutbox.EnqueueAsync(@event, cancellationToken).ConfigureAwait(false);

            foreach (var eventMetadata in eventHandlers)
            {
                if (eventMetadata.IsDeferred)
                {
                    if (eventOutbox != null)
                        continue;

                    // Outside of a command there is no completion to wait for: deferred handlers run
                    // right away. The same applies when the inherited scope has already ended (a
                    // fire-and-forget task publishing after its command completed) — queueing into a
                    // dead scope would silently drop the event.
                    if (scope != null && scope.TryAddDeferred(new DeferredEvent(@event, eventMetadata)))
                        continue;
                }

                await InvokeHandlerAsync(eventMetadata, @event, cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        #region IDomainEventDispatcher members

        public async Task DispatchDeferredAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            if (!options.TryGetEventHandlers(@event.GetType(), out var eventHandlers))
                return;

            foreach (var eventMetadata in eventHandlers)
            {
                if (!eventMetadata.IsDeferred)
                    continue;

                // Exceptions propagate: the outbox processor decides whether to retry the delivery.
                await InvokeHandlerAsync(eventMetadata, @event, cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        IEventOutbox? ResolveOutbox()
        {
            // Explicit opt-in (AddEventOutbox) gates the rerouting; a store registered without it
            // changes nothing, and the flag without a store fails loudly right here.
            if (!options.UseEventOutbox)
                return null;

            return outbox ??= serviceProvider.GetRequiredService<IEventOutbox>();
        }

        static bool HasDeferredHandler(IReadOnlyList<EventMetadata> eventHandlers)
        {
            foreach (var eventMetadata in eventHandlers)
            {
                if (eventMetadata.IsDeferred)
                    return true;
            }

            return false;
        }

        internal void BeginCommand()
        {
            var parent = currentScope.Value;
            if (parent != null && !parent.IsOwnedBy(this))
                parent = null;

            currentScope.Value = new CommandScope(this, parent);
        }

        internal async Task EndCommandAsync(bool success)
        {
            var scope = currentScope.Value ?? throw new InvalidOperationException("No command scope to end.");

            // Deferred handlers publishing during the flush below see the parent scope (or none).
            currentScope.Value = scope.Parent;

            // Marks the scope ended: a fire-and-forget publish that inherited it will run its
            // deferred handlers immediately instead of queueing into a dead scope.
            var pending = scope.TakeDeferred();

            // The facts did not survive the failed command: their deferred handlers must not run.
            if (!success || pending.Length == 0)
                return;

            if (scope.Parent != null)
            {
                // Deferred events of a successful nested command wait for the outermost command,
                // which may still fail and discard them. A parent that has already ended (this was
                // a fire-and-forget nested command finishing late) cannot accept them — run now.
                if (scope.Parent.TryAddDeferred(pending))
                    return;
            }

            foreach (var deferredEvent in pending)
            {
                try
                {
                    // The command has already succeeded and its caller may be gone (e.g. an aborted
                    // request): the deferred work is no longer cancellable on the caller's behalf.
                    await InvokeHandlerAsync(deferredEvent.Metadata, deferredEvent.Event, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The command has already completed successfully: a deferred handler failure must
                    // not turn that into an error for the caller, nor stop the remaining handlers.
                    logger.LogError(ex, "Deferred event handler {HandlerType} failed for event {EventType}.", deferredEvent.Metadata.HandlerType, deferredEvent.Metadata.EventType);
                }
            }
        }

        async Task InvokeHandlerAsync(EventMetadata eventMetadata, IDomainEvent @event, CancellationToken cancellationToken)
        {
            using var activity = DomainDiagnostics.StartEventHandler(eventMetadata);
            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                var handlerObject = eventMetadata.CreateHandler(serviceProvider);
                try
                {
                    await eventMetadata.Invoke(handlerObject, @event, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                DomainDiagnostics.EndEventHandler(activity, eventMetadata, startTimestamp, exception);
                throw;
            }

            DomainDiagnostics.EndEventHandler(activity, eventMetadata, startTimestamp);
        }

        sealed class CommandScope(DomainEventPublisher owner, CommandScope? parent)
        {
            // Guards `deferred` and `ended`: parallel nested commands promote into the shared
            // parent concurrently, a handler may fan out parallel publishes within one command,
            // and a fire-and-forget task inheriting the scope may publish after it ended.
            readonly object gate = new();
            List<DeferredEvent>? deferred;
            bool ended;

            public CommandScope? Parent { get; } = parent;

            public bool IsOwnedBy(DomainEventPublisher publisher)
            {
                return ReferenceEquals(owner, publisher);
            }

            /// <summary>
            /// Queues a deferred event; <see langword="false"/> when the scope has already ended —
            /// the caller must execute the handler itself.
            /// </summary>
            public bool TryAddDeferred(DeferredEvent deferredEvent)
            {
                lock (gate)
                {
                    if (ended)
                        return false;

                    (deferred ??= []).Add(deferredEvent);
                    return true;
                }
            }

            public bool TryAddDeferred(IReadOnlyList<DeferredEvent> deferredEvents)
            {
                lock (gate)
                {
                    if (ended)
                        return false;

                    (deferred ??= []).AddRange(deferredEvents);
                    return true;
                }
            }

            /// <summary>
            /// Ends the scope and drains its queue. Later publishes into this scope are refused
            /// (see <see cref="TryAddDeferred(DeferredEvent)"/>).
            /// </summary>
            public DeferredEvent[] TakeDeferred()
            {
                lock (gate)
                {
                    ended = true;

                    if (deferred == null)
                        return [];

                    var pending = deferred.ToArray();
                    deferred = null;
                    return pending;
                }
            }
        }

        sealed record DeferredEvent(IDomainEvent Event, EventMetadata Metadata);
    }
}
