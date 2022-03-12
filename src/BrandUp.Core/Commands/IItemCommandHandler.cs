using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Commands
{
    public interface IItemCommandHandler<in TItem, in TCommand>
        where TCommand : IItemCommand<TItem>
    {
        Task<Result> HandleAsync(TItem item, TCommand command, CancellationToken cancelationToken = default);
    }

    public interface IItemCommandHandler<in TItem, in TCommand, TResult>
        where TCommand : IItemCommand<TItem, TResult>
    {
        Task<Result<TResult>> HandleAsync(TItem item, TCommand command, CancellationToken cancelationToken = default);
    }
}