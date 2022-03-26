using BrandUp.Builder;
using BrandUp.Items;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp
{
    public static class DomainBuilderExtensions
    {
        readonly static Type ItemProviderDefinitionType = typeof(IItemProvider<,>);

        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddScoped<IValidator, TValidator>();

            return builder;
        }

        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var providerType = typeof(TProvider);

            foreach (var providerInterface in providerType.GetInterfaces())
            {
                if (!providerInterface.IsGenericType)
                    continue;

                if (ItemProviderDefinitionType == providerInterface.GetGenericTypeDefinition())
                {
                    builder.Services.AddScoped(providerType);
                    builder.Services.AddScoped(providerInterface, provider =>
                    {
                        return provider.GetRequiredService<TProvider>();
                    });

                    return builder;
                }
            }

            throw new InvalidOperationException($"Type \"{providerType.AssemblyQualifiedName}\" is do not implementation interface {ItemProviderDefinitionType.FullName}.");
        }
    }
}