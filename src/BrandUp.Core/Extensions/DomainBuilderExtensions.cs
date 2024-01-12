using System;
using BrandUp.Builder;
using BrandUp.Decorators;
using BrandUp.Items;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    public static class DomainBuilderExtensions
    {
        readonly static Type ItemProviderDefinitionType = typeof(IItemProvider<,>);

        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            ArgumentNullException.ThrowIfNull(nameof(builder));

            builder.Services.AddScoped<IValidator, TValidator>();

            return builder;
        }

        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(nameof(builder));

            var providerType = typeof(TProvider);

            foreach (var providerInterface in providerType.GetInterfaces())
            {
                if (!providerInterface.IsGenericType)
                    continue;

                if (ItemProviderDefinitionType == providerInterface.GetGenericTypeDefinition())
                {
                    builder.Services.AddScoped(providerType);
                    builder.Services.AddScoped(providerInterface, provider => provider.GetRequiredService<TProvider>());

                    return builder;
                }
            }

            throw new InvalidOperationException($"Type \"{providerType.AssemblyQualifiedName}\" is do not implementation interface {ItemProviderDefinitionType.FullName}.");
        }

        public static IDomainBuilder AddDecorators(this IDomainBuilder builder, Action<DecoratorOptions> options)
        {
            ArgumentNullException.ThrowIfNull(nameof(builder));

            if (options != null)
                builder.Services.Configure(options);

            return builder;
        }
    }
}