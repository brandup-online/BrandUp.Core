using BrandUp.Authorization;
using BrandUp.Behaviors;
using BrandUp.Caching;
using BrandUp.Idempotency;
using BrandUp.Serialization;
using BrandUp.Events;
using BrandUp.Transactions;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IDomainBuilder"/> extensions for registering domain infrastructure:
    /// validators, behaviors, transactions, query caching, the event outbox, the error catalog
    /// and item providers.
    /// </summary>
    public static class DomainBuilderExtensions
    {
        /// <summary>
        /// Registers a validator that runs for every dispatched command and query, next to the
        /// default <see cref="ComponentModelValidator"/>. Registering the same validator type
        /// twice keeps a single registration.
        /// </summary>
        /// <typeparam name="TValidator">Validator implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            ArgumentNullException.ThrowIfNull(builder);

            // TryAddEnumerable: composed or repeated registrations must not run the same
            // validator twice and duplicate its errors.
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator, TValidator>());

            return builder;
        }

        /// <summary>
        /// Registers a dispatch behavior (middleware around every query and command). Behaviors run
        /// in registration order, after the built-in validation behavior; the first registered is
        /// the outermost.
        /// </summary>
        /// <typeparam name="TBehavior">Behavior implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddBehavior<TBehavior>(this IDomainBuilder builder)
            where TBehavior : class, IDomainBehavior
        {
            ArgumentNullException.ThrowIfNull(builder);

            // TryAddEnumerable: composed or repeated registrations must not stack duplicate
            // pipeline stages (e.g. two TransactionBehaviors beginning two transactions).
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainBehavior, TBehavior>());

            return builder;
        }

        /// <summary>
        /// Wraps command dispatch in transactions from an already-registered
        /// <see cref="ITransactionFactory"/> (see <see cref="TransactionBehavior"/>).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddTransactions(this IDomainBuilder builder)
        {
            return builder.AddBehavior<TransactionBehavior>();
        }

        /// <summary>
        /// Registers the transaction factory as scoped and wraps command dispatch in its
        /// transactions (see <see cref="TransactionBehavior"/>).
        /// </summary>
        /// <typeparam name="TFactory">Transaction factory implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddTransactions<TFactory>(this IDomainBuilder builder)
            where TFactory : class, ITransactionFactory
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddScoped<ITransactionFactory, TFactory>();

            return builder.AddBehavior<TransactionBehavior>();
        }

        /// <summary>
        /// Enables caching of queries declaring <see cref="ICachedQuery"/>, backed by the
        /// in-process <see cref="MemoryQueryCache"/> unless an <see cref="IQueryCache"/> is
        /// already registered (see <see cref="QueryCacheBehavior"/>). The behavior takes the
        /// registration-order position like any other; a cache hit short-circuits behaviors
        /// registered after it, so call this after authorization-like behaviors. Invalidation
        /// runs at the completion of the outermost command (after the transaction commit)
        /// regardless of order.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddQueryCaching(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IQueryCache, MemoryQueryCache>();

            return builder.AddBehavior<QueryCacheBehavior>();
        }

        /// <summary>
        /// Enables caching of queries declaring <see cref="ICachedQuery"/> with a custom
        /// <see cref="IQueryCache"/> implementation.
        /// </summary>
        /// <typeparam name="TCache">Cache implementation, registered as singleton unless an
        /// <see cref="IQueryCache"/> is already registered.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddQueryCaching<TCache>(this IDomainBuilder builder)
            where TCache : class, IQueryCache
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IQueryCache, TCache>();

            return builder.AddBehavior<QueryCacheBehavior>();
        }

        /// <summary>
        /// Routes deferred domain events through the registered <see cref="IEventOutbox"/> and
        /// registers the store as scoped. Without this explicit call, registering an outbox
        /// implementation does not reroute events.
        /// </summary>
        /// <typeparam name="TOutbox">Outbox store implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddEventOutbox<TOutbox>(this IDomainBuilder builder)
            where TOutbox : class, IEventOutbox
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddScoped<IEventOutbox, TOutbox>();

            return builder.UseEventOutbox();
        }

        /// <summary>
        /// Routes deferred domain events through an <see cref="IEventOutbox"/> registered
        /// separately - a pure opt-in flag that registers nothing itself. Enqueueing a deferred
        /// event then fails loudly when no store is registered.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder UseEventOutbox(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<DomainOptions>(options => options.UseEventOutbox = true);

            return builder;
        }

        /// <summary>
        /// Runs registered <see cref="IDomainAuthorizer{TRequest}"/> implementations before the
        /// handler (see <see cref="AuthorizationBehavior"/>). Must be called before
        /// <see cref="AddQueryCaching(IDomainBuilder)"/>: a cache hit short-circuits behaviors
        /// registered after it, and serving cached data without the authorization check is a
        /// security bug — the ordering is enforced, not just documented.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="AddQueryCaching(IDomainBuilder)"/> was already called.</exception>
        public static IDomainBuilder AddAuthorization(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (builder.Services.Any(descriptor => !descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(IDomainBehavior)
                    && descriptor.ImplementationType == typeof(QueryCacheBehavior)))
            {
                throw new InvalidOperationException(
                    $"{nameof(AddAuthorization)} must be called before {nameof(AddQueryCaching)}: a cache hit short-circuits later behaviors, so caching registered first would serve cached data to unauthorized callers.");
            }

            return builder.AddBehavior<AuthorizationBehavior>();
        }

        /// <summary>
        /// Registers an authorizer for every <see cref="IDomainAuthorizer{TRequest}"/> interface
        /// it implements. Requires <see cref="AddAuthorization"/>; repeated registrations of the
        /// same authorizer type are ignored.
        /// </summary>
        /// <typeparam name="TAuthorizer">A type implementing one or more <see cref="IDomainAuthorizer{TRequest}"/> interfaces.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The type implements no authorizer interface.</exception>
        public static IDomainBuilder AddAuthorizer<TAuthorizer>(this IDomainBuilder builder)
            where TAuthorizer : class
        {
            ArgumentNullException.ThrowIfNull(builder);

            var authorizerType = typeof(TAuthorizer);
            var registered = false;
            foreach (var authorizerInterface in authorizerType.GetInterfaces())
            {
                if (!authorizerInterface.IsGenericType || authorizerInterface.GetGenericTypeDefinition() != typeof(IDomainAuthorizer<>))
                    continue;

                builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(authorizerInterface, authorizerType));
                registered = true;
            }

            if (!registered)
                throw new InvalidOperationException($"Type \"{authorizerType.AssemblyQualifiedName}\" does not implement interface {typeof(IDomainAuthorizer<>).FullName}.");

            return builder;
        }

        /// <summary>
        /// Cancels dispatches of requests declaring <see cref="IDispatchTimeout"/> when the
        /// declared duration elapses, returning the <see cref="DomainErrors.DispatchTimeout"/>
        /// error (see <see cref="TimeoutBehavior"/>). Call before
        /// <see cref="AddTransactions(IDomainBuilder)"/> so the transaction lives inside the
        /// timeout.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddDispatchTimeouts(this IDomainBuilder builder)
        {
            return builder.AddBehavior<TimeoutBehavior>();
        }

        /// <summary>
        /// Deduplicates commands declaring <see cref="IIdempotentCommand"/>, backed by the
        /// in-process <see cref="InMemoryIdempotencyStore"/> unless an
        /// <see cref="IIdempotencyStore"/> is already registered (see
        /// <see cref="IdempotencyBehavior"/> for the semantics and ordering guidance).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddIdempotency(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

            return builder.AddBehavior<IdempotencyBehavior>();
        }

        /// <summary>
        /// Deduplicates commands declaring <see cref="IIdempotentCommand"/> with a custom
        /// <see cref="IIdempotencyStore"/> implementation.
        /// </summary>
        /// <typeparam name="TStore">Store implementation, registered as singleton unless an
        /// <see cref="IIdempotencyStore"/> is already registered.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddIdempotency<TStore>(this IDomainBuilder builder)
            where TStore : class, IIdempotencyStore
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IIdempotencyStore, TStore>();

            return builder.AddBehavior<IdempotencyBehavior>();
        }

        /// <summary>
        /// Enables caching of queries declaring <see cref="ICachedQuery"/> in the registered
        /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> (Redis, SQL
        /// Server, ...), serializing results with <see cref="IResultSerializer"/> (JSON unless a
        /// custom serializer is registered) — see <see cref="DistributedQueryCache"/>. The
        /// distributed cache itself must be registered separately (e.g.
        /// <c>AddStackExchangeRedisCache</c>).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddDistributedQueryCaching(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IResultSerializer, JsonResultSerializer>();

            return builder.AddQueryCaching<DistributedQueryCache>();
        }

        /// <summary>
        /// Fails at startup — instead of at the first dispatch — on configuration the domain
        /// cannot run with: an enabled event outbox without an <see cref="Events.IEventOutbox"/>,
        /// transactions without an <see cref="Transactions.ITransactionFactory"/>, or request
        /// types carrying a marker whose behavior is not registered (<see cref="ICachedQuery"/>
        /// without <see cref="AddQueryCaching(IDomainBuilder)"/>,
        /// <see cref="IIdempotentCommand"/> without <see cref="AddIdempotency(IDomainBuilder)"/>,
        /// <see cref="IDispatchTimeout"/> without <see cref="AddDispatchTimeouts"/>).
        /// Checks run against the final registrations, so the call order relative to other
        /// <c>Add*</c> calls does not matter.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder ValidateOnStart(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // The validator holds the live service collection: it sees registrations made after
            // this call. TryAddEnumerable keeps repeated calls from stacking validators.
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DomainOptions>>(new DomainOptionsStartupValidator(builder.Services)));
            builder.Services.AddOptions<DomainOptions>().ValidateOnStart();

            return builder;
        }

        /// <summary>
        /// Registers the <see cref="ErrorCatalog"/> singleton and configures it. Repeated calls
        /// configure the same catalog, so duplicate codes fail at registration time. A catalog
        /// registered any other way (by type or factory) is rejected loudly instead of silently
        /// splitting the codes across two instances.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <param name="configure">Populates the catalog (Add/AddFrom/AddFromAssembly).</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="ErrorCatalog"/> is registered without an instance.</exception>
        public static IDomainBuilder AddErrorCatalog(this IDomainBuilder builder, Action<ErrorCatalog> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            var registration = builder.Services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ErrorCatalog) && !descriptor.IsKeyedService);

            ErrorCatalog catalog;
            if (registration == null)
            {
                catalog = new ErrorCatalog();
                builder.Services.AddSingleton(catalog);
            }
            else
            {
                catalog = registration.ImplementationInstance as ErrorCatalog
                    ?? throw new InvalidOperationException($"{nameof(ErrorCatalog)} is already registered by type or factory; populate that catalog instead of calling {nameof(AddErrorCatalog)}.");
            }

            configure(catalog);

            return builder;
        }

        /// <summary>
        /// Registers an item provider (see <see cref="ServiceCollectionExtensions.AddItemProvider{TProvider}"/>).
        /// </summary>
        /// <typeparam name="TProvider">A type implementing <see cref="Items.IItemProvider{TId, TItem}"/>.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddItemProvider<TProvider>();

            return builder;
        }
    }
}