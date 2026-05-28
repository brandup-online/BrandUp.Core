using BrandUp.Builder;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IDomainBuilder"/> extensions for registering validators and item providers.
    /// </summary>
    public static class DomainBuilderExtensions
    {
        /// <summary>
        /// Registers a validator that runs for every dispatched command and query.
        /// </summary>
        /// <typeparam name="TValidator">Validator implementation.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddValidator<TValidator>(this IDomainBuilder builder)
            where TValidator : class, IValidator
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddScoped<IValidator, TValidator>();

            return builder;
        }

        /// <summary>
        /// Registers an item provider (see <see cref="IServiceCollectionExtensions.AddDomainItem{TProvider}"/>).
        /// </summary>
        /// <typeparam name="TProvider">A type implementing <see cref="Items.IItemProvider{TId, TItem}"/>.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddItemProvider<TProvider>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddDomainItem<TProvider>();

            return builder;
        }
    }
}