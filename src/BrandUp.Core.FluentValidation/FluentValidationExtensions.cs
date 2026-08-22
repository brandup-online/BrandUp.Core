using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrandUp
{
    /// <summary>
    /// Registration extensions for the FluentValidation adapter.
    /// </summary>
    public static class FluentValidationExtensions
    {
        /// <summary>
        /// Runs the FluentValidation validators registered for each dispatched query and command
        /// inside the domain validation pipeline, next to the default
        /// <see cref="ComponentModelValidator"/> (see <see cref="FluentValidationAdapter"/>).
        /// The validators themselves are registered separately — e.g.
        /// <c>services.AddValidatorsFromAssemblyContaining&lt;SignUpCommandValidator&gt;()</c>.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddFluentValidation(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator, FluentValidationAdapter>());

            return builder;
        }
    }
}
