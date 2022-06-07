using BrandUp.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp
{
    public static class IServiceCollectionExtensions
    {
        public static IDomainBuilder AddDomain(this IServiceCollection services, Action<DomainOptions> buildAction = null)
        {
            var builder = new DomainBuilder(services);

            if (buildAction != null)
                services.Configure(buildAction);

            return builder;
        }
    }
}