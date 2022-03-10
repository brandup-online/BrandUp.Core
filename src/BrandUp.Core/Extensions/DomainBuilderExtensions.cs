using BrandUp.Builder;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp
{
    public static class DomainBuilderExtensions
    {
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddScoped<IValidator, TValidator>();

            return builder;
        }
    }
}