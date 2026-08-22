using BrandUp.Validation;

namespace BrandUp.Behaviors
{
    /// <summary>
    /// Built-in first behavior: runs every registered <see cref="IValidator"/> against the request
    /// and short-circuits with the validation errors before the handler is reached.
    /// </summary>
    public sealed class ValidationBehavior : IDomainBehavior
    {
        readonly IValidator[] validators;

        /// <summary>
        /// Creates the behavior over the validators of the executing scope.
        /// </summary>
        /// <param name="validators">Registered validators.</param>
        public ValidationBehavior(IEnumerable<IValidator> validators)
        {
            ArgumentNullException.ThrowIfNull(validators);

            // Dedupe by implementation type once - the set is fixed for the scope's lifetime.
            // TryAddEnumerable cannot see a validator the app registered on the service
            // collection directly (e.g. the default ComponentModelValidator added a second
            // time), and running the same validator twice would duplicate every error it
            // reports.
            List<IValidator> unique = [];
            HashSet<Type>? seen = null;
            foreach (var validator in validators)
            {
                if ((seen ??= []).Add(validator.GetType()))
                    unique.Add(validator);
            }

            this.validators = [.. unique];
        }

        /// <inheritdoc/>
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            // No validators - a pure passthrough with no async state machine.
            if (validators.Length == 0)
                return next();

            return InvokeCoreAsync(context, next, cancellationToken);
        }

        async Task<Result> InvokeCoreAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken)
        {
            List<ValidationError>? errors = null;

            foreach (var validator in validators)
                await validator.ValidateAsync(context.Request, context.Services, errors ??= [], cancellationToken).ConfigureAwait(false);

            if (errors is { Count: > 0 })
                return context.CreateError([.. errors]);

            return await next().ConfigureAwait(false);
        }
    }
}
