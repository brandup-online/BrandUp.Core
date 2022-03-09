using BrandUp.Builder;
using BrandUp.Commands.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BrandUp
{
    public static class DomainBuilderExtensions
    {
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, ICommandValidator
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddScoped<ICommandValidator, TValidator>();

            return builder;
        }
    }
}