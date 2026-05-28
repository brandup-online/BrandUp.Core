namespace BrandUp.Validation
{
    /// <summary>
    /// Validates commands and queries before they are dispatched. All registered validators are run.
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Validates an object, appending any failures to <paramref name="errors"/>.
        /// </summary>
        /// <param name="obj">Command or query to validate.</param>
        /// <param name="serviceProvider">Provider used to resolve validation dependencies.</param>
        /// <param name="errors">Collection to append validation errors to.</param>
        /// <returns><see langword="true"/> if the object is valid.</returns>
        bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors);
    }

    /// <summary>
    /// A validation failure, optionally associated with one or more member names.
    /// </summary>
    public class CommandValidationError : Error
    {
        /// <summary>
        /// Names of the members the error relates to (may be empty).
        /// </summary>
        public IEnumerable<string> MemberNames { get; }

        /// <summary>
        /// Creates a validation error with an empty code.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="memberNames">Related member names; <see langword="null"/> is treated as empty.</param>
        public CommandValidationError(string message, IEnumerable<string>? memberNames) : base(string.Empty, message)
        {
            MemberNames = memberNames ?? [];
        }
    }
}
