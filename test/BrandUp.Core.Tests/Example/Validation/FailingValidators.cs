using BrandUp.Validation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Validation
{
    public class FailingValidatorA : IValidator
    {
        public Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default)
        {
            errors.Add(new ValidationError("error A", null));
            return Task.CompletedTask;
        }
    }

    public class FailingValidatorB : IValidator
    {
        public Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default)
        {
            errors.Add(new ValidationError("error B", null));
            return Task.CompletedTask;
        }
    }
}
