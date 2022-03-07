using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.CQRS.Example.Commands
{
    public class JoinUserCommandHandler : ICommandHandler<JoinUserCommand, JoinUserResult>
    {
        public Task<JoinUserResult> HandleAsync(JoinUserCommand command, CancellationToken cancelationToken = default)
        {
            var result = new JoinUserResult
            {
                User = new Items.User
                {
                    Id = System.Guid.NewGuid(),
                    Phone = command.Phone
                }
            };

            return Task.FromResult(result);
        }
    }

    public class JoinUserCommand : ICommand<JoinUserResult>
    {
        public string Phone { get; set; }
    }

    public class JoinUserResult
    {
        public Items.User User { get; set; }
    }
}