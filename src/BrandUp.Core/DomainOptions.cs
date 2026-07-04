using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using BrandUp.Commands;
using BrandUp.Queries;

namespace BrandUp
{
    /// <summary>
    /// Registry of query and command handlers that backs an <see cref="IDomain"/>.
    /// Configured via <c>AddDomain</c>; handlers are keyed by query type and command type.
    /// </summary>
    public class DomainOptions
    {
        internal readonly static Type QueryHandlerDefinitionType = typeof(IQueryHandler<,>);
        internal readonly static Type SingleQueryHandlerDefinitionType = typeof(ISingleQueryHandler<,>);
        internal readonly static Type CommandHandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        internal readonly static Type CommandHandlerNotResultDefinitionType = typeof(ICommandHandler<>);
        internal readonly static Type ItemCommandHandlerWithResultDefinitionType = typeof(IItemCommandHandler<,,>);
        internal readonly static Type ItemCommandHandlerNotResultDefinitionType = typeof(IItemCommandHandler<,>);

        readonly Dictionary<Type, QueryMetadata> queries = [];
        readonly Dictionary<Type, CommandMetadata> commands = [];

        FrozenDictionary<Type, QueryMetadata>? frozenQueries;
        FrozenDictionary<Type, CommandMetadata>? frozenCommands;

        /// <summary>
        /// Registers a query handler.
        /// </summary>
        /// <typeparam name="THandler">A type implementing <see cref="IQueryHandler{TQuery, TRow}"/>.</typeparam>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The type is not a query handler, or a handler for the same query type is already registered.
        /// </exception>
        public DomainOptions AddQuery<THandler>()
        {
            var handlerType = typeof(THandler);

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
                    throw new InvalidOperationException($"Query handler \"{handlerType.AssemblyQualifiedName}\" already exist.");

                frozenQueries = null;

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" is do not implementation interface {QueryHandlerDefinitionType.FullName} or {SingleQueryHandlerDefinitionType.FullName}.");
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
            var handlerType = typeof(THandler);

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
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by command type \"{commandType.AssemblyQualifiedName}\".");

                frozenCommands = null;

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" is do not implementation interface {CommandHandlerWithResultDefinitionType.FullName}.");
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
    }
}
