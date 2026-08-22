using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Behaviors
{
    /// <summary>
    /// Built-in first behavior: runs every registered <see cref="IValidator"/> against the request
    /// and short-circuits with the validation errors before the handler is reached.
    /// </summary>
    public sealed class ValidationBehavior : IDomainBehavior
    {
        /// <inheritdoc/>
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            List<ValidationError>? errors = null;
            HashSet<Type>? seenValidators = null;

            foreach (var validator in context.Services.GetServices<IValidator>())
            {
                // Dedupe by implementation type: TryAddEnumerable cannot see a validator the app
                // registered on the service collection directly (e.g. the default
                // ComponentModelValidator added a second time), and running the same validator
                // twice would duplicate every error it reports.
                if (!(seenValidators ??= []).Add(validator.GetType()))
                    continue;

                await validator.ValidateAsync(context.Request, context.Services, errors ??= [], cancellationToken).ConfigureAwait(false);
            }

            if (errors is { Count: > 0 })
                return context.CreateError([.. errors]);

            return await next().ConfigureAwait(false);
        }
    }
}
