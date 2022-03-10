using BrandUp.Commands;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public class JoinUserCommandHandler : ICommandHandler<JoinUserCommand, JoinUserResult>
    {
        public Task<Result<JoinUserResult>> HandleAsync(JoinUserCommand command, CancellationToken cancelationToken = default)
        {
            var result = new JoinUserResult
            {
                User = new Items.User
                {
                    Id = System.Guid.NewGuid(),
                    Phone = command.Phone
                }
            };

            return Task.FromResult(Result.Success(result));
        }
    }

    public class JoinUserCommand : ICommand<JoinUserResult>
    {
        [Required]
        public string Phone { get; set; }
    }

    public class JoinUserResult
    {
        public Items.User User { get; set; }
    }
}