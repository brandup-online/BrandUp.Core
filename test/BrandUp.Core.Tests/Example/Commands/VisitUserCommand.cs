using BrandUp.Commands;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public class VisitUserCommandHandler : ICommandHandler<VisitUserCommand>
    {
        public Task<Result> HandleAsync(VisitUserCommand command, CancellationToken cancelationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    public class VisitUserCommand : ICommand
    {
        [Required]
        public string Phone { get; set; }
    }
}