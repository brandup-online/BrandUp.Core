using BrandUp.Validation;
using System;
using System.Collections.Generic;

namespace BrandUp.Example.Validation
{
    public class FailingValidatorA : IValidator
    {
        public bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors)
        {
            errors.Add(new CommandValidationError("error A", null));
            return false;
        }
    }

    public class FailingValidatorB : IValidator
    {
        public bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors)
        {
            errors.Add(new CommandValidationError("error B", null));
            return false;
        }
    }
}
