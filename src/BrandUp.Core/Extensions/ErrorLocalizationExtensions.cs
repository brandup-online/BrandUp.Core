using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace BrandUp
{
    /// <summary>
    /// Registration extensions for error localization.
    /// </summary>
    public static class ErrorLocalizationExtensions
    {
        /// <summary>
        /// Registers <see cref="StringErrorLocalizer"/> as the <see cref="IErrorLocalizer"/>
        /// (with <c>AddLocalization</c>): localized message templates live in the .resx files of
        /// <typeparamref name="TResource"/>, keyed by error code.
        /// </summary>
        /// <typeparam name="TResource">Resource marker type the .resx files are attached to.</typeparam>
        /// <param name="services">Service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection AddErrorLocalization<TResource>(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddLocalization();
            services.TryAddSingleton<IErrorLocalizer>(provider => new StringErrorLocalizer(provider.GetRequiredService<IStringLocalizer<TResource>>()));

            return services;
        }

        /// <summary>
        /// Registers <see cref="StringErrorLocalizer"/> as the <see cref="IErrorLocalizer"/>
        /// on the domain builder, keeping the configuration chain (see
        /// <see cref="AddErrorLocalization{TResource}(IServiceCollection)"/>).
        /// </summary>
        /// <typeparam name="TResource">Resource marker type the .resx files are attached to.</typeparam>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddErrorLocalization<TResource>(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddErrorLocalization<TResource>();

            return builder;
        }
    }
}
