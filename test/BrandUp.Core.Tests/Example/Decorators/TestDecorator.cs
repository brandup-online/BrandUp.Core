using System.Threading;
using System.Threading.Tasks;
using BrandUp.Decorators;
using BrandUp.Example.Commands;

namespace BrandUp.Example.Decorators
{
    internal class TestDecorator : ICommandDecorator
    {
        public Task<Result> BeforeExecuteCommandAsync(object command, CancellationToken cancellationToken)
        {
            var joinUserCommand = (JoinUserCommand)command;
            joinUserCommand.Phone = "79881221223";

            return Task.FromResult(Result.Success());
        }

        public Task<Result> AfterExecuteCommandAsync(object command, Result result, CancellationToken cancellationToken)
        {
            var joinUserCommand = (JoinUserCommand)command;
            joinUserCommand.Phone = "79999999999";

            return Task.FromResult(Result.Success());
        }
    }
}
