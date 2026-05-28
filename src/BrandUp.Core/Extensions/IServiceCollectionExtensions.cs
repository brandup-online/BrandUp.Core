using BrandUp.Builder;
using BrandUp.Items;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions for registering the domain and its item providers.
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        readonly static Type ItemProviderDefinitionType = typeof(IItemProvider<,>);

        /// <summary>
        /// Registers the domain (<see cref="IDomain"/>) and configures its handlers.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="buildAction">Optional callback to register query and command handlers.</param>
        /// <returns>A builder for further configuration.</returns>
        public static IDomainBuilder AddDomain(this IServiceCollection services, Action<DomainOptions>? buildAction = null)
        {
            var builder = new DomainBuilder(services);

            if (buildAction != null)
                services.Configure(buildAction);

            return builder;
        }

        /// <summary>
        /// Adds additional domain configuration after <see cref="AddDomain"/> has been called.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Callback to register more handlers.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection ConfigureDomain(this IServiceCollection services, Action<DomainOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);

            return services;
        }

        /// <summary>
        /// Registers an item provider as scoped, both by its concrete type and its
        /// <see cref="IItemProvider{TId, TItem}"/> interface.
        /// </summary>
        /// <typeparam name="TProvider">A type implementing <see cref="IItemProvider{TId, TItem}"/>.</typeparam>
        /// <param name="services">Service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The type does not implement an item provider interface.</exception>
        public static IServiceCollection AddDomainItem<TProvider>(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var providerType = typeof(TProvider);

            foreach (var providerInterface in providerType.GetInterfaces())
            {
                if (!providerInterface.IsGenericType)
                    continue;

                if (ItemProviderDefinitionType == providerInterface.GetGenericTypeDefinition())
                {
                    services.AddScoped(providerType);
                    services.AddScoped(providerInterface, provider => provider.GetRequiredService(providerType));

                    return services;
                }
            }

            throw new InvalidOperationException($"Type \"{providerType.AssemblyQualifiedName}\" is do not implementation interface {ItemProviderDefinitionType.FullName}.");
        }
    }
}