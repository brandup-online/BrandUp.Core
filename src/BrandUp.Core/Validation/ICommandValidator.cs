namespace BrandUp.Validation
{
    public interface IValidator
    {
        bool Validate(object obj, IServiceProvider serviceProvider, IList<CommandValidationError> errors);
    }

    public class CommandValidationError : Error
    {
        public IEnumerable<string> MemberNames { get; }

        public CommandValidationError(string message, IEnumerable<string> memberNames) : base(string.Empty, message)
        {
            MemberNames = memberNames ?? System.Linq.Enumerable.Empty<string>();
        }
    }
}