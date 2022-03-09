using System;
using System.Collections.Generic;

namespace BrandUp.Commands.Validation
{
    public interface ICommandValidator
    {
        bool Validate(ICommand command, IServiceProvider serviceProvider, IList<CommandValidationError> errors);
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