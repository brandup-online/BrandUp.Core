using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Behaviors
{
    /// <summary>
    /// Built-in first behavior: runs every registered <see cref="IValidator"/> against the request
    /// and short-circuits with the validation errors before the handler is reached.
    /// </summary>
    public class ValidationBehavior : IDomainBehavior
    {
        /// <inheritdoc/>
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            List<CommandValidationError>? errors = null;

            foreach (var validator in context.Services.GetServices<IValidator>())
                validator.Validate(context.Request, context.Services, errors ??= []);

            if (errors is { Count: > 0 })
                return context.CreateError([.. errors]);

            return await next().ConfigureAwait(false);
        }
    }
}
