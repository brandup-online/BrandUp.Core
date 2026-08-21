using System.Reflection;

namespace BrandUp.Events
{
    /// <summary>
    /// Reflection metadata describing a registered event handler and a cached invoker for its <c>HandleAsync</c> method.
    /// </summary>
    public class EventMetadata
    {
        readonly Func<object, object?, object, CancellationToken, object> invoker;
        readonly Func<IServiceProvider, object> handlerFactory;

        /// <summary>
        /// Concrete handler type.
        /// </summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Concrete event type the handler handles.
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// The handler's <c>HandleAsync</c> method.
        /// </summary>
        public MethodInfo HandleMethod { get; }

        /// <summary>
        /// <see langword="true"/> when the handler execution is deferred until the outermost domain
        /// command completes successfully (see <see cref="IDeferredDomainEventHandler{TEvent}"/>).
        /// </summary>
        public bool IsDeferred { get; }

        EventMetadata(Type handlerType, Type eventType, MethodInfo handleMethod, bool isDeferred, Func<object, object?, object, CancellationToken, object> invoker, Func<IServiceProvider, object> handlerFactory)
        {
            HandlerType = handlerType;
            EventType = eventType;
            HandleMethod = handleMethod;
            IsDeferred = isDeferred;
            this.invoker = invoker;
            this.handlerFactory = handlerFactory;
        }

        internal object CreateHandler(IServiceProvider serviceProvider)
        {
            return handlerFactory(serviceProvider);
        }

        internal Task Invoke(object handler, object @event, CancellationToken cancellationToken)
        {
            return (Task)invoker(handler, null, @event, cancellationToken);
        }

        internal static EventMetadata Build(Type handlerType, Type handlerInterface, Type eventType, bool isDeferred)
        {
            var handleMethod = HandlerActivator.GetHandleMethod(handlerInterface, [eventType, typeof(CancellationToken)]);
            var invoker = HandlerActivator.BuildInvoker(handlerInterface, handleMethod, null, eventType);
            var handlerFactory = HandlerActivator.BuildFactory(handlerType);

            return new EventMetadata(handlerType, eventType, handleMethod, isDeferred, invoker, handlerFactory);
        }
    }
}
