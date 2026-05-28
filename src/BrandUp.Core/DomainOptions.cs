using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using BrandUp.Commands;
using BrandUp.Queries;

namespace BrandUp
{
    public class DomainOptions
    {
        internal readonly static Type QueryHandlerDefinitionType = typeof(IQueryHandler<,>);
        internal readonly static Type CommandHandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        internal readonly static Type CommandHandlerNotResultDefinitionType = typeof(ICommandHandler<>);
        internal readonly static Type ItemCommandHandlerWithResultDefinitionType = typeof(IItemCommandHandler<,,>);
        internal readonly static Type ItemCommandHandlerNotResultDefinitionType = typeof(IItemCommandHandler<,>);

        readonly Dictionary<Type, QueryMetadata> queries = [];
        readonly Dictionary<Type, CommandMetadata> commandsWithResults = [];
        readonly Dictionary<Type, CommandMetadata> commandsNotResults = [];

        FrozenDictionary<Type, QueryMetadata>? frozenQueries;
        FrozenDictionary<Type, CommandMetadata>? frozenCommandsWithResults;
        FrozenDictionary<Type, CommandMetadata>? frozenCommandsNotResults;

        public DomainOptions AddQuery<THandler>()
        {
            var handlerType = typeof(THandler);

            foreach (var iType in handlerType.GetInterfaces())
            {
                if (!iType.IsGenericType)
                    continue;

                if (QueryHandlerDefinitionType == iType.GetGenericTypeDefinition())
                {
                    var queryMetadata = QueryMetadata.Build(handlerType, iType);

                    if (!queries.TryAdd(queryMetadata.QueryType, queryMetadata))
                        throw new InvalidOperationException($"Query handler \"{handlerType.AssemblyQualifiedName}\" already exist.");

                    frozenQueries = null;

                    return this;
                }
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" is do not implementation interface {QueryHandlerDefinitionType.FullName}.");
        }
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

                if (resultType != null && !commandsWithResults.TryAdd(resultType, commandMetadata))
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by result type \"{resultType.AssemblyQualifiedName}\".");
                else if (!commandsNotResults.TryAdd(commandType, commandMetadata))
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by command type \"{commandType.AssemblyQualifiedName}\".");

                frozenCommandsWithResults = null;
                frozenCommandsNotResults = null;

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" is do not implementation interface {CommandHandlerWithResultDefinitionType.FullName}.");
        }

        public bool TryGetQueryHandler(Type queryType, [MaybeNullWhen(false)] out QueryMetadata queryMetadata)
        {
            frozenQueries ??= queries.ToFrozenDictionary();
            return frozenQueries.TryGetValue(queryType, out queryMetadata);
        }
        public bool TryGetHandlerWithResult<TResult>([MaybeNullWhen(false)] out CommandMetadata commandMetadata)
        {
            frozenCommandsWithResults ??= commandsWithResults.ToFrozenDictionary();
            return frozenCommandsWithResults.TryGetValue(typeof(TResult), out commandMetadata);
        }
        public bool TryGetHandlerNotResult(Type commandType, [MaybeNullWhen(false)] out CommandMetadata commandMetadata)
        {
            frozenCommandsNotResults ??= commandsNotResults.ToFrozenDictionary();
            return frozenCommandsNotResults.TryGetValue(commandType, out commandMetadata);
        }
    }
}
