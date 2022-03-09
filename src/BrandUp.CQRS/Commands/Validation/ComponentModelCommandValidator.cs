using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BrandUp.Commands.Validation
{
    public class ComponentModelCommandValidator : ICommandValidator
    {
        public bool Validate(ICommand command, IServiceProvider serviceProvider, IList<CommandValidationError> errors)
        {
            var vc = new ValidationContext(command);
            vc.InitializeServiceProvider(serviceProvider.GetService);

            var result = new List<ValidationResult>();
            var isSuccess = Validator.TryValidateObject(command, vc, result, false);

            foreach (var ve in result)
                errors.Add(new CommandValidationError(ve.ErrorMessage, ve.MemberNames));

            return isSuccess;
        }
    }
}