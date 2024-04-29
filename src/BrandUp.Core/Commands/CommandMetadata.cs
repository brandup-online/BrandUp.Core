using System.Collections.ObjectModel;
using System.Reflection;

namespace BrandUp.Commands
{
    public class CommandMetadata
    {
        ConstructorInfo constructor;
        IReadOnlyCollection<Type> constructorParamTypes;
        MethodInfo handleMethod;

        public Type HandlerType { get; private set; }
        public Type ItemType { get; private set; }
        public Type CommandType { get; private set; }
        public Type ResultType { get; private set; }
        public ConstructorInfo Constructor => constructor;
        public IReadOnlyCollection<Type> ConstructorParamTypes => constructorParamTypes;
        public MethodInfo HandleMethod => handleMethod;
        public bool IsForItem => ItemType != null;
        public bool WithResult => ResultType != null;

        internal static CommandMetadata Build(Type handlerType, Type handlerInterface, Type itemType, Type commandType, Type resultType)
        {
            var commandMetadata = new CommandMetadata
            {
                HandlerType = handlerType,
                ItemType = itemType,
                CommandType = commandType,
                ResultType = resultType
            };

            var constructors = handlerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length > 1)
                throw new InvalidOperationException();
            else if (constructors.Length == 0)
                throw new InvalidOperationException();

            commandMetadata.constructor = constructors[0];

            var constructorParams = commandMetadata.constructor.GetParameters();
            commandMetadata.constructorParamTypes = new ReadOnlyCollection<Type>(constructorParams.Select(it => it.ParameterType).ToList());

            Type[] methodParamTypes;
            if (commandMetadata.IsForItem)
                methodParamTypes = [itemType, commandMetadata.CommandType, typeof(CancellationToken)];
            else
                methodParamTypes = [commandMetadata.CommandType, typeof(CancellationToken)];

            commandMetadata.handleMethod = handlerInterface.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase, null, methodParamTypes, null);
            if (commandMetadata.handleMethod == null)
                throw new InvalidOperationException();

            return commandMetadata;
        }
    }
}