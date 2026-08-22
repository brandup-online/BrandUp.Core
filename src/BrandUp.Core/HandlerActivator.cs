using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    /// <summary>
    /// Shared machinery behind handler metadata: resolves <c>HandleAsync</c> on a closed handler
    /// interface, compiles an untyped invoker for it, caches a constructor factory and disposes
    /// handler instances. Used by command, query and event metadata alike.
    /// </summary>
    internal static class HandlerActivator
    {
        // NonPublic is required: explicitly implemented interface methods are non-public on the interface map.
        public static MethodInfo GetHandleMethod(Type handlerInterface, Type[] parameterTypes)
        {
            return handlerInterface.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null)
                ?? throw new InvalidOperationException($"Handler interface \"{handlerInterface.AssemblyQualifiedName}\" does not declare a \"HandleAsync\" method.");
        }

        /// <summary>
        /// Compiles <c>(handler, item, argument, token) => ((THandlerInterface)handler).HandleAsync(...)</c>.
        /// The <c>item</c> parameter is ignored when <paramref name="itemType"/> is <see langword="null"/>.
        /// </summary>
        public static Func<object, object?, object, CancellationToken, object> BuildInvoker(Type handlerInterface, MethodInfo handleMethod, Type? itemType, Type argumentType)
        {
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var itemParam = Expression.Parameter(typeof(object), "item");
            var argumentParam = Expression.Parameter(typeof(object), "argument");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var instance = Expression.Convert(handlerParam, handlerInterface);

            MethodCallExpression call;
            if (itemType != null)
                call = Expression.Call(instance, handleMethod,
                    Expression.Convert(itemParam, itemType),
                    Expression.Convert(argumentParam, argumentType),
                    cancellationTokenParam);
            else
                call = Expression.Call(instance, handleMethod,
                    Expression.Convert(argumentParam, argumentType),
                    cancellationTokenParam);

            return Expression.Lambda<Func<object, object?, object, CancellationToken, object>>(
                Expression.Convert(call, typeof(object)),
                handlerParam, itemParam, argumentParam, cancellationTokenParam).Compile();
        }

        /// <summary>
        /// Builds a cached constructor factory for the handler type, avoiding constructor
        /// selection on every dispatch. Types the factory cannot be built for statically
        /// (e.g. several public constructors) fall back to per-dispatch
        /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/>,
        /// which picks the best DI-resolvable constructor per call — the historical behavior.
        /// The fallback also keeps construction errors at dispatch time, so one bad handler
        /// does not poison the whole registration.
        /// </summary>
        public static Func<IServiceProvider, object> BuildFactory(Type handlerType)
        {
            ObjectFactory objectFactory;
            try
            {
                objectFactory = ActivatorUtilities.CreateFactory(handlerType, Type.EmptyTypes);
            }
            catch (InvalidOperationException)
            {
                return serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, handlerType);
            }

            return serviceProvider => objectFactory(serviceProvider, []);
        }

        public static async ValueTask DisposeHandlerAsync(object handler)
        {
            if (handler is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (handler is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
