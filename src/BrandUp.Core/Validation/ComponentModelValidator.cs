using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BrandUp.Validation
{
    public class ComponentModelValidator : IValidator
    {
        public bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors)
        {
            var vc = new ValidationContext(obj);
            vc.InitializeServiceProvider(serviceProvider.GetService);

            var result = new List<ValidationResult>();
            var isSuccess = Validator.TryValidateObject(obj, vc, result, false);

            foreach (var ve in result)
                errors.Add(new CommandValidationError(ve.ErrorMessage, ve.MemberNames));

            return isSuccess;
        }
    }
}