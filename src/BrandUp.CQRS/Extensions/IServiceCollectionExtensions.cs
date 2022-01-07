using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.CQRS
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddCQRS(this IServiceCollection services)
        {
            return services;
        }
    }
}