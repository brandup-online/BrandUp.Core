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
            var options = new DomainOptions();

            services.Configure(buildAction);

            return builder;
        }
    }
}