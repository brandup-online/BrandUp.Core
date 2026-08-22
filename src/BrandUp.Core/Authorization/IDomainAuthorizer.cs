using BrandUp.Behaviors;

namespace BrandUp.Authorization
{
    /// <summary>
    /// Authorizes dispatches of <typeparamref name="TRequest"/> before the handler runs. Routed
    /// by the exact runtime type of the request (like event handlers): an authorizer registered
    /// for a base type or interface does not run for derived requests — a class guarding several
    /// request types implements this interface for each of them and registers once via
    /// <see cref="DomainBuilderExtensions.AddAuthorizer{TAuthorizer}"/>. Requires
    /// <see cref="DomainBuilderExtensions.AddAuthorization"/>.
    /// </summary>
    /// <typeparam name="TRequest">Exact type of the guarded query or command.</typeparam>
    public interface IDomainAuthorizer<TRequest>
    {
        /// <summary>
        /// Authorizes the dispatch: a successful <see cref="Result"/> lets it proceed, a failed
        /// one short-circuits the pipeline with the returned errors (use
        /// <see cref="ErrorKind.Unauthorized"/>/<see cref="ErrorKind.Forbidden"/>, e.g.
        /// <see cref="DomainErrors.AccessDenied"/>).
        /// </summary>
        /// <param name="request">The query or command being dispatched.</param>
        /// <param name="context">Dispatch context (kind, item, services).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<Result> AuthorizeAsync(TRequest request, DomainBehaviorContext context, CancellationToken cancellationToken = default);
    }
}
