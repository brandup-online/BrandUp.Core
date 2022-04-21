using BrandUp.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp
{
    public static class IServiceCollectionExtensions
    {
        public static IDomainBuilder AddDomain(this IServiceCollection services, Action<DomainOptions> buildAction)
        {
            var builder = new DomainBuilder(services);

            services.Configure(buildAction);

            return builder;
        }
    }
}