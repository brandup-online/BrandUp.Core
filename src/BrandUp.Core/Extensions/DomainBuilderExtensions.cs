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
        /// already registered (see <see cref="QueryCacheBehavior"/>).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddQueryCaching(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddSingleton<IQueryCache, MemoryQueryCache>();
            AddQueryCacheBehavior(builder.Services);

            return builder;
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
            AddQueryCacheBehavior(builder.Services);

            return builder;
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

        static void AddQueryCacheBehavior(IServiceCollection services)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(IDomainBehavior) && descriptor.ImplementationType == typeof(QueryCacheBehavior)))
                return;

            var cacheDescriptor = ServiceDescriptor.Scoped<IDomainBehavior, QueryCacheBehavior>();

            // Cache invalidation must run after the transaction commit: keep the cache behavior
            // outside TransactionBehavior regardless of the registration order the caller used.
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(IDomainBehavior) && services[i].ImplementationType == typeof(TransactionBehavior))
                {
                    services.Insert(i, cacheDescriptor);
                    return;
                }
            }

            services.Add(cacheDescriptor);
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