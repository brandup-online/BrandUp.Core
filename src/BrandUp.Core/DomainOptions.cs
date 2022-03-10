using BrandUp.Commands;
using BrandUp.Queries;
using System;
using System.Collections.Generic;

namespace BrandUp
{
    public class DomainOptions
    {
        readonly static Type QueryHandlerDefinitionType = typeof(IQueryHandler<,>);
        readonly static Type CommandHandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        readonly static Type CommandHandlerNotResultDefinitionType = typeof(ICommandHandler<>);

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

                    break;
                }
            }

            return this;
        }
        public DomainOptions AddCommand<THandler>()
        {
            var handlerType = typeof(THandler);

            foreach (var iType in handlerType.GetInterfaces())
            {
                if (!iType.IsGenericType)
                    continue;

                if (CommandHandlerWithResultDefinitionType == iType.GetGenericTypeDefinition())
                {
                    var commandMedata = CommandMetadata.Build(handlerType, iType);

                    if (!commandsWithResults.TryAdd(commandMedata.ResultType, commandMedata))
                        throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by result type \"{commandMedata.ResultType.AssemblyQualifiedName}\".");

                    break;
                }
                else if (CommandHandlerNotResultDefinitionType == iType.GetGenericTypeDefinition())
                {
                    var commandMedata = CommandMetadata.Build(handlerType, iType);

                    if (!commandsNotResults.TryAdd(commandMedata.CommandType, commandMedata))
                        throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by command type \"{commandMedata.CommandType.AssemblyQualifiedName}\".");
                    break;
                }
            }

            return this;
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