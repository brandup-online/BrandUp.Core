using System.ComponentModel.DataAnnotations;

namespace BrandUp.Validation
{
    /// <summary>
    /// <see cref="IValidator"/> based on <see cref="System.ComponentModel.DataAnnotations"/>
    /// attributes. Registered by default by <c>AddDomain</c>.
    /// </summary>
    public sealed class ComponentModelValidator : IValidator
    {
        /// <inheritdoc/>
        public Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(errors);

            var validationContext = new ValidationContext(request);
            validationContext.InitializeServiceProvider(serviceProvider.GetService);

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, validationContext, results, true);

            foreach (var validationResult in results)
                errors.Add(new ValidationError(validationResult.ErrorMessage!, validationResult.MemberNames));

            return Task.CompletedTask;
        }
    }
}
