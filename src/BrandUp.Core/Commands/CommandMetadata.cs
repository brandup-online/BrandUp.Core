using System.Linq.Expressions;
using System.Reflection;

namespace BrandUp.Commands
{
    /// <summary>
    /// Reflection metadata describing a registered command handler and a cached invoker for its <c>HandleAsync</c> method.
    /// </summary>
    public class CommandMetadata
    {
        readonly Func<object, object?, object, CancellationToken, object> invoker;

        /// <summary>
        /// Concrete handler type.
        /// </summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Target item type for item commands; <see langword="null"/> for plain commands.
        /// </summary>
        public Type? ItemType { get; }

        /// <summary>
        /// Concrete command type the handler handles.
        /// </summary>
        public Type CommandType { get; }

        /// <summary>
        /// Result data type for commands with a result; <see langword="null"/> otherwise.
        /// </summary>
        public Type? ResultType { get; }

        /// <summary>
        /// The handler's <c>HandleAsync</c> method.
        /// </summary>
        public MethodInfo HandleMethod { get; }

        /// <summary>
        /// <see langword="true"/> when the command targets an item (see <see cref="ItemType"/>).
        /// </summary>
        public bool IsForItem => ItemType != null;

        /// <summary>
        /// <see langword="true"/> when the command produces result data (see <see cref="ResultType"/>).
        /// </summary>
        public bool WithResult => ResultType != null;

        CommandMetadata(Type handlerType, Type? itemType, Type commandType, Type? resultType, MethodInfo handleMethod, Func<object, object?, object, CancellationToken, object> invoker)
        {
            HandlerType = handlerType;
            ItemType = itemType;
            CommandType = commandType;
            ResultType = resultType;
            HandleMethod = handleMethod;
            this.invoker = invoker;
        }

        internal object Invoke(object handler, object? item, object command, CancellationToken cancellationToken)
        {
            return invoker(handler, item, command, cancellationToken);
        }

        internal static CommandMetadata Build(Type handlerType, Type handlerInterface, Type? itemType, Type commandType, Type? resultType)
        {
            Type[] methodParamTypes = itemType != null
                ? [itemType, commandType, typeof(CancellationToken)]
                : [commandType, typeof(CancellationToken)];

            var handleMethod = handlerInterface.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, methodParamTypes, null)
                ?? throw new InvalidOperationException($"Not found \"HandleAsync\" method on command handler interface \"{handlerInterface.AssemblyQualifiedName}\".");

            var invoker = BuildInvoker(handlerInterface, handleMethod, itemType, commandType);

            return new CommandMetadata(handlerType, itemType, commandType, resultType, handleMethod, invoker);
        }

        static Func<object, object?, object, CancellationToken, object> BuildInvoker(Type handlerInterface, MethodInfo handleMethod, Type? itemType, Type commandType)
        {
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var itemParam = Expression.Parameter(typeof(object), "item");
            var commandParam = Expression.Parameter(typeof(object), "command");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var instance = Expression.Convert(handlerParam, handlerInterface);

            MethodCallExpression call;
            if (itemType != null)
                call = Expression.Call(instance, handleMethod,
                    Expression.Convert(itemParam, itemType),
                    Expression.Convert(commandParam, commandType),
                    cancellationTokenParam);
            else
                call = Expression.Call(instance, handleMethod,
                    Expression.Convert(commandParam, commandType),
                    cancellationTokenParam);

            return Expression.Lambda<Func<object, object?, object, CancellationToken, object>>(
                Expression.Convert(call, typeof(object)),
                handlerParam, itemParam, commandParam, cancellationTokenParam).Compile();
        }
    }
}
