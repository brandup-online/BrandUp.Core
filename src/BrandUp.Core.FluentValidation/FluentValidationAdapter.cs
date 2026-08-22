using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Validation
{
    /// <summary>
    /// Bridges FluentValidation into the domain validation pipeline: every
    /// <see cref="FluentValidation.IValidator{T}"/> registered for the dispatched request's exact
    /// runtime type runs, and its failures are reported as <see cref="ValidationError"/>s with
    /// their member names. Register via <c>AddFluentValidation()</c>; the FluentValidation
    /// validators themselves are registered the usual way (e.g.
    /// <c>AddValidatorsFromAssemblyContaining</c> from FluentValidation.DependencyInjectionExtensions).
    /// </summary>
    public sealed class FluentValidationAdapter : IValidator
    {
        static readonly ConcurrentDictionary<Type, Type> validatorServiceTypes = new();

        /// <inheritdoc/>
        public async Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(errors);

            var serviceType = validatorServiceTypes.GetOrAdd(request.GetType(),
                static requestType => typeof(FluentValidation.IValidator<>).MakeGenericType(requestType));

            IValidationContext? validationContext = null;
            foreach (var service in serviceProvider.GetServices(serviceType))
            {
                // AbstractValidator<T> accepts a non-generic context whose instance is a T -
                // the request's runtime type closed the service type above.
                validationContext ??= new ValidationContext<object>(request);

                var validationResult = await ((FluentValidation.IValidator)service!).ValidateAsync(validationContext, cancellationToken).ConfigureAwait(false);

                foreach (var failure in validationResult.Errors)
                {
                    errors.Add(new ValidationError(
                        failure.ErrorMessage,
                        string.IsNullOrEmpty(failure.PropertyName) ? null : [failure.PropertyName]));
                }
            }
        }
    }
}
