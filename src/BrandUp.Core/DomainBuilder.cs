using BrandUp.Behaviors;
using BrandUp.Events;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrandUp
{
    /// <summary>
    /// Default <see cref="IDomainBuilder"/>. Registers core domain services on construction.
    /// </summary>
    public sealed class DomainBuilder : IDomainBuilder
    {
        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Creates the builder and registers core domain services into <paramref name="services"/>.
        /// </summary>
        /// <param name="services">Service collection to populate.</param>
        public DomainBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            AddCoreServices();
        }

        internal void AddCoreServices()
        {
            var services = Services;

            services.AddScoped<IDomain, DomainImpl>();

            // First registered behavior is the outermost: validation runs before any user
            // behavior. TryAddEnumerable keeps a repeated AddDomain from stacking a second
            // validation stage.
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainBehavior, ValidationBehavior>());

            // Data-annotations validation works out of the box - the obvious code ([Required]
            // attributes plus AddDomain) must validate without an extra registration.
            // TryAddEnumerable keeps composed registrations from stacking a duplicate validator.
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator, ComponentModelValidator>());

            // Scoped: event handlers are constructed from the publishing scope's service provider.
            services.AddScoped<DomainEventPublisher>();
            services.AddScoped<IDomainEventPublisher>(provider => provider.GetRequiredService<DomainEventPublisher>());
            services.AddScoped<IDomainEventDispatcher>(provider => provider.GetRequiredService<DomainEventPublisher>());
        }
    }

    /// <summary>
    /// Fluent builder for configuring domain services.
    /// </summary>
    public interface IDomainBuilder
    {
        /// <summary>
        /// The underlying service collection.
        /// </summary>
        IServiceCollection Services { get; }
    }
}