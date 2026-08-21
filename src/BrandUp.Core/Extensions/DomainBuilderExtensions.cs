using BrandUp.Behaviors;
using BrandUp.Builder;
using BrandUp.Caching;
using BrandUp.Events;
using BrandUp.Transactions;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IDomainBuilder"/> extensions for registering validators and item providers.
    /// </summary>
    public static class DomainBuilderExtensions
    {
        /// <summary>
        /// Registers a validator that runs for every dispatched command and query.
        /// </summary>
        /// <typeparam name="TValidator">Validator implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddScoped<IValidator, TValidator>();

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
        /// <typeparam name="TCache">Cache implementation, registered as singleton.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddQueryCaching<TCache>(this IDomainBuilder builder)
            where TCache : class, IQueryCache
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddSingleton<IQueryCache, TCache>();

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

            return builder.AddEventOutbox();
        }

        /// <summary>
        /// Routes deferred domain events through an <see cref="IEventOutbox"/> registered
        /// separately. Enqueueing a deferred event then fails loudly when no store is registered.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddEventOutbox(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<DomainOptions>(options => options.UseEventOutbox = true);

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
        /// Registers an item provider (see <see cref="IServiceCollectionExtensions.AddDomainItem{TProvider}"/>).
        /// </summary>
        /// <typeparam name="TProvider">A type implementing <see cref="Items.IItemProvider{TId, TItem}"/>.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddDomainItem<TProvider>();

            return builder;
        }
    }
}