using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BrandUp.CQRS.Builder
{
    public class CQRSBuilder
    {
        readonly static Type HandlerDefinitionType = typeof(ICommandHandler<,>);
        private readonly Dictionary<Type, CommandMetadata> commands = new Dictionary<Type, CommandMetadata>();

        public IServiceCollection Services { get; }

        public CQRSBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public CQRSBuilder AddCommand<THandler>()
        {
            var handlerType = typeof(THandler);

            foreach (var iType in handlerType.GetInterfaces())
            {
                if (iType.IsGenericType && HandlerDefinitionType == iType.GetGenericTypeDefinition())
                {
                    commands.Add(handlerType, CommandMetadata.Build(handlerType));
                    break;
                }
            }

            return this;
        }
        public CQRSBuilder AddStore<TStore>()
            where TStore : class, IDomainStore
        {
            Services.AddScoped<IDomainStore, TStore>();

            return this;
        }

        public void Build()
        {

        }
    }

    internal class CommandMetadata
    {
        private ConstructorInfo constructor;
        readonly List<Type> constructorParamTypes = new List<Type>();

        public Type HandlerType { get; private set; }
        public Type CommandType { get; private set; }
        public Type ResultType { get; private set; }

        private CommandMetadata()
        {
        }

        public static CommandMetadata Build(Type handlerType)
        {
            var commandMetadata = new CommandMetadata
            {
                HandlerType = handlerType,
                CommandType = handlerType.GenericTypeArguments[0],
                ResultType = handlerType.GenericTypeArguments[1]
            };

            var constructors = handlerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length > 1)
                throw new InvalidOperationException();
            else if (constructors.Length == 0)
                throw new InvalidOperationException();

            commandMetadata.constructor = constructors[0];

            var constructorParams = commandMetadata.constructor.GetParameters();
            foreach (var p in constructorParams)
                commandMetadata.constructorParamTypes.Add(p.ParameterType);

            return commandMetadata;
        }
    }
}
