using BrandUp.CQRS.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp.CQRS
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddCQRS(this IServiceCollection services, Action<CQRSBuilder> buildAction)
        {
            var builder = new CQRSBuilder(services);

            buildAction(builder);

            builder.Build();

            services.AddScoped<IDomain, Domain>();

            return services;
        }
    }
}