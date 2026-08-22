namespace BrandUp.Behaviors
{
    /// <summary>
    /// The dispatch a behavior is wrapping: what is being executed and the services of the
    /// executing scope.
    /// </summary>
    public sealed class DomainBehaviorContext
    {
        readonly Func<IList<IError>, Result> errorFactory;
        Dictionary<object, object?>? properties;

        /// <summary>
        /// What kind of dispatch this is.
        /// </summary>
        public DomainDispatchKind Kind { get; }

        /// <summary>
        /// The query or command being dispatched.
        /// </summary>
        public object Request { get; }

        /// <summary>
        /// Target item for <see cref="DomainDispatchKind.ItemCommand"/>; <see langword="null"/> otherwise.
        /// </summary>
        public object? Item { get; }

        /// <summary>
        /// Service provider of the executing scope.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// The exact <see cref="Result"/>-derived type this dispatch returns
        /// (e.g. <c>Result&lt;IList&lt;TRow&gt;&gt;</c> for a list query).
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// <see langword="true"/> when this dispatch happens inside a command dispatch of the same
        /// async flow - e.g. a query executed by a command handler. Such a query may observe the
        /// command's uncommitted state, so caching behaviors bypass it.
        /// </summary>
        public bool IsInsideCommand { get; }

        /// <summary>
        /// Token of the current dispatch. Starts as the caller's token; a timeout behavior
        /// narrows it for everything downstream — each pipeline step and the handler read it at
        /// invocation time.
        /// </summary>
        public CancellationToken CancellationToken { get; internal set; }

        /// <summary>
        /// Arbitrary state shared between behaviors of one dispatch. Created on first access.
        /// </summary>
        public IDictionary<object, object?> Properties => properties ??= [];

        /// <summary>
        /// <see langword="true"/> for command dispatches (plain or item).
        /// </summary>
        public bool IsCommand => Kind is DomainDispatchKind.Command or DomainDispatchKind.ItemCommand;

        /// <summary>
        /// <see langword="true"/> for query dispatches (list or single-value).
        /// </summary>
        public bool IsQuery => Kind is DomainDispatchKind.Query or DomainDispatchKind.SingleQuery;

        /// <summary>
        /// Creates the context.
        /// </summary>
        /// <param name="kind">Kind of the dispatch.</param>
        /// <param name="request">The query or command being dispatched.</param>
        /// <param name="item">Target item for item commands.</param>
        /// <param name="services">Service provider of the executing scope.</param>
        /// <param name="resultType">The exact <see cref="Result"/>-derived type the dispatch returns.</param>
        /// <param name="errorFactory">Builds an error result of the shape the dispatch returns.</param>
        /// <param name="cancellationToken">Token of the dispatch (the caller's token).</param>
        public DomainBehaviorContext(DomainDispatchKind kind, object request, object? item, IServiceProvider services, Type resultType, Func<IList<IError>, Result> errorFactory, CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            Kind = kind;
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Item = item;
            Services = services ?? throw new ArgumentNullException(nameof(services));
            ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
            this.errorFactory = errorFactory ?? throw new ArgumentNullException(nameof(errorFactory));
            IsInsideCommand = CommandDispatchScope.Current != null;
        }

        /// <summary>
        /// Creates a failed result of the exact shape this dispatch returns
        /// (e.g. <c>Result&lt;IList&lt;TRow&gt;&gt;</c> for a list query) — the only valid way for a
        /// behavior to short-circuit with errors.
        /// </summary>
        /// <param name="errors">Non-empty set of errors.</param>
        public Result CreateError(IList<IError> errors)
        {
            return errorFactory(errors);
        }

        /// <summary>
        /// Creates a failed result of the exact shape this dispatch returns, from a single error.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="kind">Semantic category of the error.</param>
        public Result CreateError(string code, string message, ErrorKind kind = ErrorKind.Unspecified)
        {
            return errorFactory([new Error(code, message, kind)]);
        }
    }

    /// <summary>
    /// Kind of a domain dispatch, as seen by <see cref="IDomainBehavior"/>.
    /// </summary>
    public enum DomainDispatchKind
    {
        /// <summary>A list query (<see cref="Queries.IQuery{TRow}"/>).</summary>
        Query,

        /// <summary>A single-value query (<see cref="Queries.ISingleQuery{TModel}"/>).</summary>
        SingleQuery,

        /// <summary>A plain command (<see cref="Commands.ICommand"/> or <see cref="Commands.ICommand{TResult}"/>).</summary>
        Command,

        /// <summary>An item command (<see cref="Commands.IItemCommand{TItem}"/> or <see cref="Commands.IItemCommand{TItem, TResult}"/>).</summary>
        ItemCommand
    }
}
