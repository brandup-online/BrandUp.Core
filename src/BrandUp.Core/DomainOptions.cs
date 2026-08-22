using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Queries;

namespace BrandUp
{
    /// <summary>
    /// Registry of query, command and event handlers that backs an <see cref="IDomain"/>.
    /// Configured via <c>AddDomain</c>; handlers are keyed by query, command and event type.
    /// </summary>
    public sealed class DomainOptions
    {
        internal readonly static Type QueryHandlerDefinitionType = typeof(IQueryHandler<,>);
        internal readonly static Type SingleQueryHandlerDefinitionType = typeof(ISingleQueryHandler<,>);
        internal readonly static Type CommandHandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        internal readonly static Type CommandHandlerNotResultDefinitionType = typeof(ICommandHandler<>);
        internal readonly static Type ItemCommandHandlerWithResultDefinitionType = typeof(IItemCommandHandler<,,>);
        internal readonly static Type ItemCommandHandlerNotResultDefinitionType = typeof(IItemCommandHandler<,>);
        internal readonly static Type EventHandlerDefinitionType = typeof(IDomainEventHandler<>);
        internal readonly static Type DeferredEventHandlerDefinitionType = typeof(IDeferredDomainEventHandler<>);

        readonly Dictionary<Type, QueryMetadata> queries = [];

        internal IEnumerable<Type> QueryTypes => queries.Keys;

        internal IEnumerable<Type> CommandTypes => commands.Keys;
        readonly Dictionary<Type, CommandMetadata> commands = [];
        readonly Dictionary<Type, List<EventMetadata>> events = [];

        FrozenDictionary<Type, QueryMetadata>? frozenQueries;
        FrozenDictionary<Type, CommandMetadata>? frozenCommands;
        FrozenDictionary<Type, IReadOnlyList<EventMetadata>>? frozenEvents;

        /// <summary>
        /// When <see langword="true"/>, publishing an event with no registered handlers throws
        /// instead of being a no-op. Off by default: broadcast semantics allow optional subscribers.
        /// </summary>
        public bool RequireEventHandlers { get; set; }

        /// <summary>
        /// <see langword="true"/> when at least one deferred event handler is registered; command
        /// dispatch skips the deferral machinery entirely otherwise.
        /// </summary>
        internal bool HasDeferredEventHandlers { get; private set; }

        /// <summary>
        /// <see langword="true"/> when deferred events are routed through the registered
        /// <see cref="Events.IEventOutbox"/>. Enabled explicitly via
        /// <see cref="DomainBuilderExtensions.UseEventOutbox(IDomainBuilder)"/> — merely
        /// registering an outbox implementation does not reroute events.
        /// </summary>
        internal bool UseEventOutbox { get; set; }

        // Single source of handler-interface classification, shared by the explicit Add methods
        // and by AddHandlersFrom - so a new handler interface cannot be registered by one path
        // and silently skipped by the scan.
        internal static bool IsQueryHandlerInterface(Type handlerInterface)
        {
            if (!handlerInterface.IsGenericType)
                return false;

            var definition = handlerInterface.GetGenericTypeDefinition();
            return definition == QueryHandlerDefinitionType || definition == SingleQueryHandlerDefinitionType;
        }

        internal static bool IsCommandHandlerInterface(Type handlerInterface)
        {
            if (!handlerInterface.IsGenericType)
                return false;

            var definition = handlerInterface.GetGenericTypeDefinition();
            return definition == CommandHandlerWithResultDefinitionType || definition == CommandHandlerNotResultDefinitionType
                || definition == ItemCommandHandlerWithResultDefinitionType || definition == ItemCommandHandlerNotResultDefinitionType;
        }

        internal static bool IsEventHandlerInterface(Type handlerInterface)
        {
            return handlerInterface.IsGenericType && handlerInterface.GetGenericTypeDefinition() == EventHandlerDefinitionType;
        }

        /// <summary>
        /// Registers a query handler.
        /// </summary>
        /// <typeparam name="THandler">A type implementing <see cref="IQueryHandler{TQuery, TRow}"/>
        /// or <see cref="ISingleQueryHandler{TQuery, TModel}"/>.</typeparam>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The type is not a query handler, or a handler for the same query type is already registered.
        /// </exception>
        public DomainOptions AddQuery<THandler>()
        {
            return AddQuery(typeof(THandler));
        }

        /// <summary>
        /// Registers a query handler by type (see <see cref="AddQuery{THandler}"/>).
        /// </summary>
        /// <param name="handlerType">A type implementing <see cref="IQueryHandler{TQuery, TRow}"/> or <see cref="ISingleQueryHandler{TQuery, TModel}"/>.</param>
        /// <returns>This instance, for chaining.</returns>
        public DomainOptions AddQuery(Type handlerType)
        {
            ArgumentNullException.ThrowIfNull(handlerType);

            foreach (var iType in handlerType.GetInterfaces())
            {
                if (!iType.IsGenericType)
                    continue;

                var genericTypeDefinition = iType.GetGenericTypeDefinition();

                bool isSingle;
                if (genericTypeDefinition == QueryHandlerDefinitionType)
                    isSingle = false;
                else if (genericTypeDefinition == SingleQueryHandlerDefinitionType)
                    isSingle = true;
                else
                    continue;

                var queryMetadata = QueryMetadata.Build(handlerType, iType, isSingle);

                if (!queries.TryAdd(queryMetadata.QueryType, queryMetadata))
                    throw new InvalidOperationException($"Query handler \"{handlerType.AssemblyQualifiedName}\" is already registered.");

                frozenQueries = null;

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" does not implement interface {QueryHandlerDefinitionType.FullName} or {SingleQueryHandlerDefinitionType.FullName}.");
        }
        /// <summary>
        /// Registers a command handler (with or without result, and item or non-item).
        /// </summary>
        /// <typeparam name="THandler">
        /// A type implementing one of <see cref="ICommandHandler{TCommand}"/>,
        /// <see cref="ICommandHandler{TCommand, TResult}"/>, <see cref="IItemCommandHandler{TItem, TCommand}"/>
        /// or <see cref="IItemCommandHandler{TItem, TCommand, TResult}"/>.
        /// </typeparam>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The type is not a command handler, or a handler for the same command type is already registered.
        /// </exception>
        public DomainOptions AddCommand<THandler>()
        {
            return AddCommand(typeof(THandler));
        }

        /// <summary>
        /// Registers a command handler by type (see <see cref="AddCommand{THandler}"/>).
        /// </summary>
        /// <param name="handlerType">A type implementing one of the command handler interfaces.</param>
        /// <returns>This instance, for chaining.</returns>
        public DomainOptions AddCommand(Type handlerType)
        {
            ArgumentNullException.ThrowIfNull(handlerType);

            foreach (var handlerInterface in handlerType.GetInterfaces())
            {
                if (!handlerInterface.IsGenericType)
                    continue;

                var genericTypeDefinition = handlerInterface.GetGenericTypeDefinition();

                Type? itemType;
                Type commandType;
                Type? resultType;
                if (genericTypeDefinition == CommandHandlerWithResultDefinitionType)
                {
                    itemType = null;
                    commandType = handlerInterface.GenericTypeArguments[0];
                    resultType = handlerInterface.GenericTypeArguments[1];
                }
                else if (genericTypeDefinition == ItemCommandHandlerWithResultDefinitionType)
                {
                    itemType = handlerInterface.GenericTypeArguments[0];
                    commandType = handlerInterface.GenericTypeArguments[1];
                    resultType = handlerInterface.GenericTypeArguments[2];
                }
                else if (genericTypeDefinition == CommandHandlerNotResultDefinitionType)
                {
                    itemType = null;
                    commandType = handlerInterface.GenericTypeArguments[0];
                    resultType = null;
                }
                else if (genericTypeDefinition == ItemCommandHandlerNotResultDefinitionType)
                {
                    itemType = handlerInterface.GenericTypeArguments[0];
                    commandType = handlerInterface.GenericTypeArguments[1];
                    resultType = null;
                }
                else
                    continue;

                var commandMetadata = CommandMetadata.Build(handlerType, handlerInterface, itemType, commandType, resultType);

                if (!commands.TryAdd(commandType, commandMetadata))
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" is already registered for command type \"{commandType.AssemblyQualifiedName}\".");

                frozenCommands = null;

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" does not implement interface {CommandHandlerWithResultDefinitionType.FullName}.");
        }

        /// <summary>
        /// Registers an event handler. Unlike commands, an event may have several handlers, and one
        /// handler class may handle several event types — it is registered for each of them.
        /// </summary>
        /// <typeparam name="THandler">
        /// A type implementing <see cref="IDomainEventHandler{TEvent}"/> (or
        /// <see cref="IDeferredDomainEventHandler{TEvent}"/> for deferred execution) for one or more event types.
        /// </typeparam>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The type is not an event handler, or it is already registered for one of its event types.
        /// </exception>
        public DomainOptions AddEvent<THandler>()
        {
            return AddEvent(typeof(THandler));
        }

        /// <summary>
        /// Registers an event handler by type (see <see cref="AddEvent{THandler}"/>).
        /// </summary>
        /// <param name="handlerType">A type implementing <see cref="IDomainEventHandler{TEvent}"/> for one or more event types.</param>
        /// <returns>This instance, for chaining.</returns>
        public DomainOptions AddEvent(Type handlerType)
        {
            ArgumentNullException.ThrowIfNull(handlerType);

            var handlerInterfaces = handlerType.GetInterfaces();

            // Validate and build first, mutate after: a duplicate must not leave the registry
            // partially updated for a multi-event handler.
            var registrations = new List<EventMetadata>();
            foreach (var handlerInterface in handlerInterfaces)
            {
                if (!handlerInterface.IsGenericType || handlerInterface.GetGenericTypeDefinition() != EventHandlerDefinitionType)
                    continue;

                var eventType = handlerInterface.GenericTypeArguments[0];

                if (events.TryGetValue(eventType, out var registered) && registered.Exists(m => m.HandlerType == handlerType))
                    throw new InvalidOperationException($"Event handler \"{handlerType.AssemblyQualifiedName}\" is already registered for event type \"{eventType.AssemblyQualifiedName}\".");

                // Exact declared-interface match: assignability would also match variant-compatible event types.
                var isDeferred = Array.IndexOf(handlerInterfaces, DeferredEventHandlerDefinitionType.MakeGenericType(eventType)) >= 0;

                registrations.Add(EventMetadata.Build(handlerType, handlerInterface, eventType, isDeferred));
            }

            if (registrations.Count == 0)
                throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" does not implement interface {EventHandlerDefinitionType.FullName}.");

            foreach (var eventMetadata in registrations)
            {
                if (!events.TryGetValue(eventMetadata.EventType, out var eventHandlers))
                    events.Add(eventMetadata.EventType, eventHandlers = []);
                eventHandlers.Add(eventMetadata);

                if (eventMetadata.IsDeferred)
                    HasDeferredEventHandlers = true;
            }

            frozenEvents = null;

            return this;
        }

        /// <summary>
        /// Registers every query, command and event handler found in the assembly: non-abstract,
        /// non-generic classes implementing at least one handler interface. Removes the
        /// "wrote a handler, forgot to register it" failure mode. A handler already registered
        /// (manually or by a previous scan) causes the same duplicate-registration exception as
        /// the explicit Add methods.
        /// </summary>
        /// <param name="assembly">Assembly to scan.</param>
        /// <param name="typeFilter">Optional filter; a type it rejects is skipped.</param>
        /// <returns>This instance, for chaining.</returns>
        public DomainOptions AddHandlersFrom(Assembly assembly, Func<Type, bool>? typeFilter = null)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
                    continue;
                if (typeFilter != null && !typeFilter(type))
                    continue;

                bool isQueryHandler = false, isCommandHandler = false, isEventHandler = false;
                foreach (var handlerInterface in type.GetInterfaces())
                {
                    if (IsQueryHandlerInterface(handlerInterface))
                        isQueryHandler = true;
                    else if (IsCommandHandlerInterface(handlerInterface))
                        isCommandHandler = true;
                    else if (IsEventHandlerInterface(handlerInterface))
                        isEventHandler = true;
                }

                if (isQueryHandler)
                    AddQuery(type);
                if (isCommandHandler)
                    AddCommand(type);
                if (isEventHandler)
                    AddEvent(type);
            }

            return this;
        }

        /// <summary>
        /// Looks up the metadata of the handler registered for the given query type.
        /// </summary>
        /// <param name="queryType">Concrete query type.</param>
        /// <param name="queryMetadata">The found metadata, or <see langword="null"/> if not registered.</param>
        /// <returns><see langword="true"/> if a handler is registered.</returns>
        public bool TryGetQueryHandler(Type queryType, [MaybeNullWhen(false)] out QueryMetadata queryMetadata)
        {
            frozenQueries ??= queries.ToFrozenDictionary();
            return frozenQueries.TryGetValue(queryType, out queryMetadata);
        }

        /// <summary>
        /// Looks up the metadata of the handler registered for the given command type.
        /// </summary>
        /// <param name="commandType">Concrete command type.</param>
        /// <param name="commandMetadata">The found metadata, or <see langword="null"/> if not registered.</param>
        /// <returns><see langword="true"/> if a handler is registered.</returns>
        public bool TryGetCommandHandler(Type commandType, [MaybeNullWhen(false)] out CommandMetadata commandMetadata)
        {
            frozenCommands ??= commands.ToFrozenDictionary();
            return frozenCommands.TryGetValue(commandType, out commandMetadata);
        }

        /// <summary>
        /// Looks up the metadata of the handlers registered for the given event type, in registration order.
        /// </summary>
        /// <param name="eventType">Concrete event type.</param>
        /// <param name="eventHandlers">The found metadata list, or <see langword="null"/> if none are registered.</param>
        /// <returns><see langword="true"/> if at least one handler is registered.</returns>
        public bool TryGetEventHandlers(Type eventType, [MaybeNullWhen(false)] out IReadOnlyList<EventMetadata> eventHandlers)
        {
            frozenEvents ??= events.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<EventMetadata>)[.. kv.Value]);
            return frozenEvents.TryGetValue(eventType, out eventHandlers);
        }
    }
}
