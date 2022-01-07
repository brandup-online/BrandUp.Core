using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.CQRS
{
    public interface ICommandHandler<in TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        Task<TResult> HandleAsync(TCommand command, CancellationToken cancelationToken = default);
    }
}