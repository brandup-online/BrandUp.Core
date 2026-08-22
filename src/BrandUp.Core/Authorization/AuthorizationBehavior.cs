using System.Collections.Concurrent;
using System.Linq.Expressions;
using BrandUp.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Authorization
{
    /// <summary>
    /// Runs every <see cref="IDomainAuthorizer{TRequest}"/> registered for the request's exact
    /// type and short-circuits with the authorizer's errors before the handler — and before any
    /// behavior registered after this one — is reached. Registered via
    /// <see cref="DomainBuilderExtensions.AddAuthorization"/>, which enforces the one ordering
    /// rule that matters: authorization must precede query caching, or a cache hit would serve
    /// data without the check.
    /// </summary>
    public sealed class AuthorizationBehavior : IDomainBehavior
    {
        static readonly ConcurrentDictionary<Type, AuthorizerInvoker> invokers = new();

        /// <inheritdoc/>
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            var invoker = invokers.GetOrAdd(context.Request.GetType(), static requestType => AuthorizerInvoker.Build(requestType));

            // The closed IEnumerable<IDomainAuthorizer<T>> type is cached on the invoker:
            // GetServices(Type) would re-derive it through MakeGenericType on every dispatch.
            var authorizers = (IEnumerable<object?>)context.Services.GetRequiredService(invoker.EnumerableType);

            // No authorizer registered for this request type - the container hands back an empty
            // array; pass through with no async state machine.
            if (authorizers is System.Collections.ICollection { Count: 0 })
                return next();

            return InvokeCoreAsync(invoker, authorizers, context, next, cancellationToken);
        }

        static async Task<Result> InvokeCoreAsync(AuthorizerInvoker invoker, IEnumerable<object?> authorizers, DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken)
        {
            foreach (var authorizer in authorizers)
            {
                var result = await invoker.Invoke(authorizer!, context.Request, context, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Authorizer \"{authorizer!.GetType().AssemblyQualifiedName}\" returned null instead of a Result.");

                // The authorizer returns a plain Result; the dispatch may expect Result<T>, so
                // the errors are rewrapped into the exact shape of this dispatch.
                if (result is not { IsSuccess: true })
                    return context.CreateError([.. result.Errors]);
            }

            return await next().ConfigureAwait(false);
        }

        sealed class AuthorizerInvoker
        {
            public required Type ServiceType { get; init; }
            public required Type EnumerableType { get; init; }
            public required Func<object, object, DomainBehaviorContext, CancellationToken, Task<Result>> Invoke { get; init; }

            public static AuthorizerInvoker Build(Type requestType)
            {
                var serviceType = typeof(IDomainAuthorizer<>).MakeGenericType(requestType);
                var method = serviceType.GetMethod(nameof(IDomainAuthorizer<object>.AuthorizeAsync))!;

                var authorizerParameter = Expression.Parameter(typeof(object));
                var requestParameter = Expression.Parameter(typeof(object));
                var contextParameter = Expression.Parameter(typeof(DomainBehaviorContext));
                var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken));

                var call = Expression.Call(
                    Expression.Convert(authorizerParameter, serviceType),
                    method,
                    Expression.Convert(requestParameter, requestType),
                    contextParameter,
                    cancellationTokenParameter);

                var invoke = Expression.Lambda<Func<object, object, DomainBehaviorContext, CancellationToken, Task<Result>>>(
                    call, authorizerParameter, requestParameter, contextParameter, cancellationTokenParameter).Compile();

                return new AuthorizerInvoker
                {
                    ServiceType = serviceType,
                    EnumerableType = typeof(IEnumerable<>).MakeGenericType(serviceType),
                    Invoke = invoke
                };
            }
        }
    }
}
