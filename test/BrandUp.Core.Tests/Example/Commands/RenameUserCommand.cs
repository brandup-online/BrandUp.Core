using BrandUp.Commands;
using BrandUp.Example.Items;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public class RenameUserCommand : IItemCommand<User, string>
    {
        public string NewPhone { get; set; }
    }

    public class RenameUserCommandHandler : IItemCommandHandler<User, RenameUserCommand, string>
    {
        public Task<Result<string>> HandleAsync(User item, RenameUserCommand command, CancellationToken cancellationToken = default)
        {
            item.Phone = command.NewPhone;

            return Task.FromResult(Result.Success(item.Phone));
        }
    }
}
