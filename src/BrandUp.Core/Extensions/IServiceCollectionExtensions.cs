using BrandUp.Builder;
using BrandUp.Items;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    public static class IServiceCollectionExtensions
    {
        readonly static Type ItemProviderDefinitionType = typeof(IItemProvider<,>);

        public static IDomainBuilder AddDomain(this IServiceCollection services, Action<DomainOptions> buildAction = null)
        {
            var builder = new DomainBuilder(services);

            if (buildAction != null)
                services.Configure(buildAction);

            return builder;
        }

        public static IServiceCollection ConfigureDomain(this IServiceCollection services, Action<DomainOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);

            return services;
        }

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
                    services.AddScoped(providerInterface, provider => provider.GetRequiredService<TProvider>());

                    return services;
                }
            }

            throw new InvalidOperationException($"Type \"{providerType.AssemblyQualifiedName}\" is do not implementation interface {ItemProviderDefinitionType.FullName}.");
        }
    }
}