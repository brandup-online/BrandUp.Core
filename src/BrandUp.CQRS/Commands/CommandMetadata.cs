﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace BrandUp.Commands
{
    public class CommandMetadata
    {
        private ConstructorInfo constructor;
        private IReadOnlyCollection<Type> constructorParamTypes;
        private MethodInfo handleMethod;

        public Type HandlerType { get; private set; }
        public Type CommandType { get; private set; }
        public Type ResultType { get; private set; }
        public ConstructorInfo Constructor => constructor;
        public IReadOnlyCollection<Type> ConstructorParamTypes => constructorParamTypes;
        public MethodInfo HandleMethod => handleMethod;

        internal static CommandMetadata Build(Type handlerType, Type handlerInterface)
        {
            var commandMetadata = new CommandMetadata
            {
                HandlerType = handlerType,
                CommandType = handlerInterface.GenericTypeArguments[0],
                ResultType = handlerInterface.GenericTypeArguments.Length > 1 ? handlerInterface.GenericTypeArguments[1] : null
            };

            var constructors = handlerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length > 1)
                throw new InvalidOperationException();
            else if (constructors.Length == 0)
                throw new InvalidOperationException();

            commandMetadata.constructor = constructors[0];

            var constructorParams = commandMetadata.constructor.GetParameters();
            commandMetadata.constructorParamTypes = new ReadOnlyCollection<Type>(constructorParams.Select(it => it.ParameterType).ToList());

            commandMetadata.handleMethod = handlerInterface.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase, null, new Type[] { commandMetadata.CommandType, typeof(CancellationToken) }, null);
            if (commandMetadata.handleMethod == null)
                throw new InvalidOperationException();

            return commandMetadata;
        }
    }
}