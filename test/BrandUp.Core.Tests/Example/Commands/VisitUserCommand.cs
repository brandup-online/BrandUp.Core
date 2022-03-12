using BrandUp.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public class VisitUserCommand : IItemCommand<Items.User>
    {
    }

    public class VisitUserCommandHandler : IItemCommandHandler<Items.User, VisitUserCommand>
    {
        public Task<Result> HandleAsync(Items.User item, VisitUserCommand command, CancellationToken cancelationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }
}