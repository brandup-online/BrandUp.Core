using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BrandUp.Validation
{
    /// <summary>
    /// <see cref="IValidator"/> based on <see cref="System.ComponentModel.DataAnnotations"/>
    /// attributes. Registered by default by <c>AddDomain</c>. Request types without any
    /// validation attribute (and not implementing <see cref="IValidatableObject"/>) are skipped
    /// without touching reflection, so the default pipeline stays cheap for unannotated requests.
    /// </summary>
    public sealed class ComponentModelValidator : IValidator
    {
        // Whether a request type has anything DataAnnotations validation could report. Keyed by
        // type, so bounded by the application's request types.
        static readonly ConcurrentDictionary<Type, bool> validatableTypes = new();

        /// <inheritdoc/>
        public Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(errors);

            if (!validatableTypes.GetOrAdd(request.GetType(), static type => IsValidatable(type)))
                return Task.CompletedTask;

            var validationContext = new ValidationContext(request);
            validationContext.InitializeServiceProvider(serviceProvider.GetService);

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, validationContext, results, true);

            foreach (var validationResult in results)
                errors.Add(new ValidationError(validationResult.ErrorMessage!, validationResult.MemberNames));

            return Task.CompletedTask;
        }

        static bool IsValidatable(Type type)
        {
            // Mirrors what Validator.TryValidateObject(validateAllProperties: true) can surface:
            // attributes on the type, attributes on public instance properties, and
            // IValidatableObject.Validate. It does not recurse into nested objects, so neither
            // does this gate. The probe goes through TypeDescriptor - the same source Validator
            // consults - so metadata-type providers and attributes on overridden base properties
            // are seen (plain reflection IsDefined misses both). The cached answer assumes
            // TypeDescriptionProviders are registered before the type's first dispatch.
            if (typeof(IValidatableObject).IsAssignableFrom(type))
                return true;

            foreach (var attribute in TypeDescriptor.GetAttributes(type))
            {
                if (attribute is ValidationAttribute)
                    return true;
            }

            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(type))
            {
                foreach (var attribute in property.Attributes)
                {
                    if (attribute is ValidationAttribute)
                        return true;
                }
            }

            return false;
        }
    }
}
