using System.ComponentModel.DataAnnotations;

namespace BrandUp.Validation
{
    public class ComponentModelValidator : IValidator
    {
        public bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(errors);

            var vc = new ValidationContext(obj);
            vc.InitializeServiceProvider(serviceProvider.GetService);

            var result = new List<ValidationResult>();
            var isSuccess = Validator.TryValidateObject(obj, vc, result, true);

            foreach (var ve in result)
                errors.Add(new CommandValidationError(ve.ErrorMessage, ve.MemberNames));

            return isSuccess;
        }
    }
}