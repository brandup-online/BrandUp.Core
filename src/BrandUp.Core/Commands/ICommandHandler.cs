using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Commands
{
    public interface ICommandHandler<in TCommand>
        where TCommand : ICommand
    {
        Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    public interface ICommandHandler<in TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}