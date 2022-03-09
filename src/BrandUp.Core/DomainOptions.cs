using BrandUp.Commands;
using System;
using System.Collections.Generic;

namespace BrandUp
{
    public class DomainOptions
    {
        readonly static Type HandlerWithResultDefinitionType = typeof(ICommandHandler<,>);
        readonly static Type HandlerNotResultDefinitionType = typeof(ICommandHandler<>);

        private readonly Dictionary<Type, CommandMetadata> commandsWithResults = new();
        private readonly Dictionary<Type, CommandMetadata> commandsNotResults = new();

        public DomainOptions AddCommand<THandler>()
        {
            var handlerType = typeof(THandler);

            foreach (var iType in handlerType.GetInterfaces())
            {
                if (!iType.IsGenericType)
                    continue;

                if (HandlerWithResultDefinitionType == iType.GetGenericTypeDefinition())
                {
                    var commandMedata = CommandMetadata.Build(handlerType, iType);

                    if (commandsWithResults.ContainsKey(commandMedata.ResultType))
                        throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by result type \"{commandMedata.ResultType.AssemblyQualifiedName}\".");

                    commandsWithResults.Add(commandMedata.ResultType, commandMedata);
                    break;
                }
                else if (HandlerNotResultDefinitionType == iType.GetGenericTypeDefinition())
                {
                    var commandMedata = CommandMetadata.Build(handlerType, iType);

                    if (commandsNotResults.ContainsKey(commandMedata.CommandType))
                        throw new InvalidOperationException($"Command handler \"{handlerType.AssemblyQualifiedName}\" already exist by command type \"{commandMedata.CommandType.AssemblyQualifiedName}\".");

                    commandsNotResults.Add(commandMedata.CommandType, commandMedata);
                    break;
                }
            }

            return this;
        }

        public bool TryGetHandlerWithResultResult<TResult>(out CommandMetadata commandMetadata)
        {
            return commandsWithResults.TryGetValue(typeof(TResult), out commandMetadata);
        }
        public bool TryGetHandlerNotResult(Type commandType, out CommandMetadata commandMetadata)
        {
            return commandsNotResults.TryGetValue(commandType, out commandMetadata);
        }
    }
}