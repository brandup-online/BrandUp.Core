namespace BrandUp.Behaviors
{
    /// <summary>
    /// Middleware around domain dispatch. Behaviors wrap every query and command dispatched
    /// through <see cref="IDomain"/>, in registration order: the first registered behavior is the
    /// outermost. A behavior may short-circuit by returning a result without calling
    /// <c>next</c> — use <see cref="DomainBehaviorContext.CreateError(System.Collections.Generic.IList{IError})"/>
    /// to produce a result of the shape the dispatch expects. Register via
    /// <see cref="DomainBuilderExtensions.AddBehavior{TBehavior}"/>.
    /// <para>
    /// A behavior that invokes <c>next</c> more than once (retry) runs every attempt inside one
    /// command scope: deferred events published by a failed attempt stay queued and flush when a
    /// later attempt succeeds. Retry outside the domain dispatch when deferred events or
    /// transactions are involved.
    /// </para>
    /// </summary>
    public interface IDomainBehavior
    {
        /// <summary>
        /// Invokes the behavior. Call <paramref name="next"/> to continue the pipeline (the last
        /// link executes the handler) and return its result, a result derived from it, or a
        /// short-circuit result created by the context.
        /// </summary>
        /// <param name="context">The dispatch being executed.</param>
        /// <param name="next">Continues the pipeline.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Continues the dispatch pipeline and returns the dispatch result.
    /// </summary>
    public delegate Task<Result> DomainBehaviorDelegate();
}
