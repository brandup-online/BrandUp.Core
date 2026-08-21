using System.Reflection;

namespace BrandUp.Queries
{
    /// <summary>
    /// Reflection metadata describing a registered query handler and a cached invoker for its <c>HandleAsync</c> method.
    /// </summary>
    public class QueryMetadata
    {
        readonly Func<object, object?, object, CancellationToken, object> invoker;
        readonly Func<IServiceProvider, object> handlerFactory;

        /// <summary>
        /// Concrete handler type.
        /// </summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Concrete query type the handler handles.
        /// </summary>
        public Type QueryType { get; }

        /// <summary>
        /// Row type for list queries, or the returned value type for single-value queries.
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// The handler's <c>HandleAsync</c> method.
        /// </summary>
        public MethodInfo HandleMethod { get; }

        /// <summary>
        /// <see langword="true"/> when the handler implements <see cref="ISingleQueryHandler{TQuery, TResult}"/>
        /// (returns a single value); <see langword="false"/> for <see cref="IQueryHandler{TQuery, TRow}"/> (returns a list).
        /// </summary>
        public bool IsSingle { get; }

        QueryMetadata(Type handlerType, Type queryType, Type resultType, bool isSingle, MethodInfo handleMethod, Func<object, object?, object, CancellationToken, object> invoker, Func<IServiceProvider, object> handlerFactory)
        {
            HandlerType = handlerType;
            QueryType = queryType;
            ResultType = resultType;
            IsSingle = isSingle;
            HandleMethod = handleMethod;
            this.invoker = invoker;
            this.handlerFactory = handlerFactory;
        }

        internal object CreateHandler(IServiceProvider serviceProvider)
        {
            return handlerFactory(serviceProvider);
        }

        internal object Invoke(object handler, object query, CancellationToken cancellationToken)
        {
            return invoker(handler, null, query, cancellationToken);
        }

        internal static QueryMetadata Build(Type handlerType, Type handlerInterface, bool isSingle)
        {
            var queryType = handlerInterface.GenericTypeArguments[0];
            var resultType = handlerInterface.GenericTypeArguments[1];

            var handleMethod = HandlerActivator.GetHandleMethod(handlerInterface, [queryType, typeof(CancellationToken)]);
            var invoker = HandlerActivator.BuildInvoker(handlerInterface, handleMethod, null, queryType);
            var handlerFactory = HandlerActivator.BuildFactory(handlerType);

            return new QueryMetadata(handlerType, queryType, resultType, isSingle, handleMethod, invoker, handlerFactory);
        }
    }
}
