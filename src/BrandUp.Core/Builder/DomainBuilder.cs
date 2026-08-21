using BrandUp.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Builder
{
    /// <summary>
    /// Default <see cref="IDomainBuilder"/>. Registers core domain services on construction.
    /// </summary>
    public class DomainBuilder : IDomainBuilder
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

            // Scoped: event handlers are constructed from the publishing scope's service provider.
            services.AddScoped<DomainEventPublisher>();
            services.AddScoped<IDomainEventPublisher>(provider => provider.GetRequiredService<DomainEventPublisher>());
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