using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;
using System;
using System.Collections.Generic;

namespace BrandUp
{
    public class DomainOptions
    {
        internal readonly static Type QueryHandlerDefinitionType = typeof(IQueryHandler<,>);
        internal readonly static Type CommandHandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        internal readonly static Type CommandHandlerNotResultDefinitionType = typeof(ICommandHandler<>);
        internal readonly static Type ItemCommandHandlerWithResultDefinitionType = typeof(IItemCommandHandler<,,>);
        internal readonly static Type ItemCommandHandlerNotResultDefinitionType = typeof(IItemCommandHandler<,>);

        private readonly Dictionary<Type, QueryMetadata> queries = new();
        private readonly Dictionary<Type, CommandMetadata> commandsWithResults = new();
        private readonly Dictionary<Type, CommandMetadata> commandsNotResults = new();

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

                Type itemType;
                Type commandType;
                Type resultType;
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

                var commandMedata = CommandMetadata.Build(handlerType, handlerInterface, itemType, commandType, resultType);

                if (commandMedata.WithResult && !commandsWithResults.TryAdd(commandMedata.ResultType, commandMedata))
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by result type \"{commandMedata.ResultType.AssemblyQualifiedName}\".");
                else if (!commandsNotResults.TryAdd(commandMedata.CommandType, commandMedata))
                    throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by command type \"{commandMedata.CommandType.AssemblyQualifiedName}\".");

                return this;
            }

            throw new InvalidOperationException($"Type \"{handlerType.AssemblyQualifiedName}\" is do not implementation interface {CommandHandlerWithResultDefinitionType.FullName}.");
        }

        public bool TryGetQueryHandler(Type queryType, out QueryMetadata queryMetadata)
        {
            return queries.TryGetValue(queryType, out queryMetadata);
        }
        public bool TryGetHandlerWithResult<TResult>(out CommandMetadata commandMetadata)
        {
            return commandsWithResults.TryGetValue(typeof(TResult), out commandMetadata);
        }
        public bool TryGetHandlerNotResult(Type commandType, out CommandMetadata commandMetadata)
        {
            return commandsNotResults.TryGetValue(commandType, out commandMetadata);
        }
    }
}