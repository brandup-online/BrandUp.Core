namespace BrandUp.Validation
{
    /// <summary>
    /// Validates commands and queries before they are dispatched. All registered validators run;
    /// <see cref="ComponentModelValidator"/> is registered by default.
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Validates a request, appending any failures to <paramref name="errors"/>. The request
        /// is invalid when at least one error was appended.
        /// </summary>
        /// <param name="request">Command or query to validate.</param>
        /// <param name="serviceProvider">Provider used to resolve validation dependencies.</param>
        /// <param name="errors">Collection to append validation errors to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ValidateAsync(object request, IServiceProvider serviceProvider, IList<ValidationError> errors, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A validation failure, optionally associated with one or more member names.
    /// </summary>
    public class ValidationError : Error
    {
        /// <summary>
        /// Names of the members the error relates to (may be empty).
        /// </summary>
        public IEnumerable<string> MemberNames { get; }

        /// <summary>
        /// Creates a validation error with an empty code. The member names are copied: consumers
        /// enumerate them more than once (serialization, model state), which a lazy sequence
        /// would not survive.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="memberNames">Related member names; <see langword="null"/> is treated as empty.</param>
        public ValidationError(string message, IEnumerable<string>? memberNames) : base(string.Empty, message, ErrorKind.Validation)
        {
            MemberNames = memberNames is null ? [] : [.. memberNames];
        }
    }
}
