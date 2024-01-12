using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Decorators
{
    public interface ICommandDecorator
    {
        Task<Result> BeforeExecuteCommandAsync(object command, CancellationToken cancellationToken);
        Task<Result> AfterExecuteCommandAsync(object command, Result result, CancellationToken cancellationToken);
    }
}
