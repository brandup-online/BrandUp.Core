using System.Linq.Expressions;
using System.Reflection;

namespace BrandUp.Queries
{
    /// <summary>
    /// Reflection metadata describing a registered query handler and a cached invoker for its <c>HandleAsync</c> method.
    /// </summary>
    public class QueryMetadata
    {
        readonly Func<object, object, CancellationToken, object> invoker;

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

        QueryMetadata(Type handlerType, Type queryType, Type resultType, bool isSingle, MethodInfo handleMethod, Func<object, object, CancellationToken, object> invoker)
        {
            HandlerType = handlerType;
            QueryType = queryType;
            ResultType = resultType;
            IsSingle = isSingle;
            HandleMethod = handleMethod;
            this.invoker = invoker;
        }

        internal object Invoke(object handler, object query, CancellationToken cancellationToken)
        {
            return invoker(handler, query, cancellationToken);
        }

        internal static QueryMetadata Build(Type handlerType, Type handlerInterface, bool isSingle)
        {
            var queryType = handlerInterface.GenericTypeArguments[0];
            var resultType = handlerInterface.GenericTypeArguments[1];

            var handleMethod = handlerInterface.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [queryType, typeof(CancellationToken)], null)
                ?? throw new InvalidOperationException($"Not found \"HandleAsync\" method on query handler interface \"{handlerInterface.AssemblyQualifiedName}\".");

            var invoker = BuildInvoker(handlerInterface, handleMethod, queryType);

            return new QueryMetadata(handlerType, queryType, resultType, isSingle, handleMethod, invoker);
        }

        static Func<object, object, CancellationToken, object> BuildInvoker(Type handlerInterface, MethodInfo handleMethod, Type queryType)
        {
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var queryParam = Expression.Parameter(typeof(object), "query");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var call = Expression.Call(
                Expression.Convert(handlerParam, handlerInterface),
                handleMethod,
                Expression.Convert(queryParam, queryType),
                cancellationTokenParam);

            return Expression.Lambda<Func<object, object, CancellationToken, object>>(
                Expression.Convert(call, typeof(object)),
                handlerParam, queryParam, cancellationTokenParam).Compile();
        }
    }
}
