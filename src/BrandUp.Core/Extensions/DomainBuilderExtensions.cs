using BrandUp.Builder;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    public static class DomainBuilderExtensions
    {
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddScoped<IValidator, TValidator>();

            return builder;
        }

        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddDomainItem<TProvider>();

            return builder;
        }
    }
}